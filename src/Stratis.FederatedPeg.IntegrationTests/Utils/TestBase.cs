using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    public class TestBase : IDisposable
    {
        protected readonly Network mainchainNetwork;
        protected readonly FederatedPegRegTest sidechainNetwork;
        protected readonly IList<Mnemonic> mnemonics;
        protected readonly Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;
        protected readonly (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;
        protected readonly List<int> federationMemberIndexes;
        protected readonly List<string> chains;

        protected readonly IReadOnlyDictionary<string, NodeChain> MainAndSideChainNodeMap;

        private readonly NodeBuilder nodeBuilder;
        private readonly CoreNode mainUser;
        private readonly CoreNode fedMain1;
        private readonly CoreNode fedMain2;
        private readonly CoreNode fedMain3;

        private readonly SidechainNodeBuilder sidechainNodeBuilder;
        private readonly CoreNode sideUser;
        private readonly CoreNode fedSide1;
        private readonly CoreNode fedSide2;
        private readonly CoreNode fedSide3;

        private const string ConfigSideChain = "sidechain";

        protected enum Chain
        {
            Main,
            Side
        }

        protected class NodeChain
        {
            public CoreNode Node { get; private set; }
            public Chain ChainType { get; private set; }

            public NodeChain(CoreNode node, Chain chainType)
            {
                this.Node = node;
                this.ChainType = chainType;
            }
        }

        public TestBase()
        {
            this.mainchainNetwork = Networks.Stratis.Regtest();
            this.sidechainNetwork = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            this.mnemonics = this.sidechainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = this.GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMemberIndexes = Enumerable.Range(0, this.pubKeysByMnemonic.Count).ToList();
            this.chains = new[] { "mainchain", "sidechain" }.ToList();

            this.nodeBuilder = NodeBuilder.Create(this);
            this.mainUser = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.mainUser));
            this.fedMain1 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain1));
            this.fedMain2 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain2));
            this.fedMain3 = this.nodeBuilder.CreateStratisPosNode(this.mainchainNetwork, nameof(this.fedMain3));

            this.sidechainNodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this);

            this.sidechainNodeBuilder.ConfigParameters.Add(ConfigSideChain, "1");
            this.sidechainNodeBuilder.ConfigParameters.Add(FederationGatewaySettings.RedeemScriptParam, this.scriptAndAddresses.payToMultiSig.ToString());

            this.sideUser = this.nodeBuilder.CreateStratisPosNode(this.sidechainNetwork);

            this.sidechainNodeBuilder.ConfigParameters.Add(FederationGatewaySettings.PublicKeyParam, this.pubKeysByMnemonic[this.mnemonics[0]].ToString());
            this.fedSide1 = this.sidechainNodeBuilder.CreateSidechainNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[0]);

            this.sidechainNodeBuilder.ConfigParameters.AddOrReplace(FederationGatewaySettings.PublicKeyParam, this.pubKeysByMnemonic[this.mnemonics[1]].ToString());
            this.fedSide2 = this.sidechainNodeBuilder.CreateSidechainNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[1]);

            this.sidechainNodeBuilder.ConfigParameters.AddOrReplace(FederationGatewaySettings.PublicKeyParam, this.pubKeysByMnemonic[this.mnemonics[2]].ToString());
            this.fedSide3 = this.sidechainNodeBuilder.CreateSidechainNode(this.sidechainNetwork, this.sidechainNetwork.FederationKeys[2]);

            this.ApplyFederationIPs(this.fedMain1, this.fedMain2, this.fedMain3);
            this.ApplyFederationIPs(this.fedSide1, this.fedSide2, this.fedSide3);

            this.ApplyCounterChainAPIPort(this.fedMain1, this.fedSide1);
            this.ApplyCounterChainAPIPort(this.fedMain2, this.fedSide2);
            this.ApplyCounterChainAPIPort(this.fedMain3, this.fedSide3);

            this.MainAndSideChainNodeMap = new Dictionary<string, NodeChain>()
            {
                { nameof(this.mainUser), new NodeChain(this.mainUser, Chain.Main) },
                { nameof(this.fedMain1), new NodeChain(this.fedMain1, Chain.Main) },
                { nameof(this.fedMain2), new NodeChain(this.fedMain2, Chain.Main) },
                { nameof(this.fedMain3), new NodeChain(this.fedMain3, Chain.Main) },
                { nameof(this.sideUser), new NodeChain(this.sideUser, Chain.Side) },
                { nameof(this.fedSide1), new NodeChain(this.fedSide1, Chain.Side) },
                { nameof(this.fedSide2), new NodeChain(this.fedSide2, Chain.Side) },
                { nameof(this.fedSide3), new NodeChain(this.fedSide3, Chain.Side) }
            };
        }

        protected (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }

        protected void StartAndConnectNodes()
        {
            this.StartNodes(Chain.Main);
            this.StartNodes(Chain.Side);

            TestHelper.WaitLoop(() =>
            {
                return this.fedMain3.State == CoreNodeState.Running &&
                        this.fedSide3.State == CoreNodeState.Running;
            });

            this.ConnectMainChainNodes();
            this.ConnectSideChainNodes();
        }

        protected void StartNodes(Chain chainType)
        {
            try
            {
                this.MainAndSideChainNodeMap.
                    Where(m => m.Value.ChainType == chainType).
                    Select(x => x.Value.Node).ToList().
                    ForEach(m => m.Start());
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void ConnectMainChainNodes()
        {
            try
            {
                TestHelper.Connect(this.mainUser, this.fedMain1);
                TestHelper.Connect(this.mainUser, this.fedMain2);
                TestHelper.Connect(this.mainUser, this.fedMain3);
                TestHelper.Connect(this.fedMain1, this.fedMain2);
                TestHelper.Connect(this.fedMain1, this.fedMain3);
                TestHelper.Connect(this.fedMain2, this.fedMain1);
                TestHelper.Connect(this.fedMain2, this.fedMain3);
                TestHelper.Connect(this.fedMain3, this.fedMain1);
                TestHelper.Connect(this.fedMain3, this.fedMain2);
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void ConnectSideChainNodes()
        {
            try
            {
                TestHelper.Connect(this.sideUser, this.fedSide1);
                TestHelper.Connect(this.sideUser, this.fedSide2);
                TestHelper.Connect(this.sideUser, this.fedSide3);
                TestHelper.Connect(this.fedSide1, this.fedSide2);
                TestHelper.Connect(this.fedSide1, this.fedSide3);
                TestHelper.Connect(this.fedSide2, this.fedSide1);
                TestHelper.Connect(this.fedSide2, this.fedSide3);
                TestHelper.Connect(this.fedSide3, this.fedSide1);
                TestHelper.Connect(this.fedSide3, this.fedSide2);

            }
            catch (Exception)
            {
                throw;
            }
        }

        private void CreateNodesWithExtraConfig()
        {

        }

        private void ApplyFederationIPs(CoreNode fed1, CoreNode fed2, CoreNode fed3)
        {
            string fedIps = $"{fed1.Endpoint},{fed2.Endpoint},{fed3.Endpoint}";

            this.AppendToConfig(fed1, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            this.AppendToConfig(fed2, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            this.AppendToConfig(fed3, $"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
        }


        private void ApplyCounterChainAPIPort(CoreNode fromNode, CoreNode toNode)
        {
            this.AppendToConfig(fromNode, $"{FederationGatewaySettings.CounterChainApiPortParam}={toNode.ApiPort.ToString()}");
            this.AppendToConfig(toNode, $"{FederationGatewaySettings.CounterChainApiPortParam}={fromNode.ApiPort.ToString()}");
        }

        private void AppendToConfig(CoreNode node, string configKeyValueIten)
        {
            using (StreamWriter sw = File.AppendText(node.Config))
            {
                sw.WriteLine(configKeyValueIten);
            }
        }

        public void Dispose()
        {
            this.nodeBuilder?.Dispose();
            this.sidechainNodeBuilder?.Dispose();
        }
    }
}
