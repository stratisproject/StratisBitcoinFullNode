using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg;
using Stratis.FederatedPeg.Features.FederationGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedSidechains.IntegrationTests
{
    public class GatewayIntegrationTestEnvironment : IDisposable
    {
        public IDictionary<Chain, Network> Networks { get; }
        public IDictionary<Chain, Mnemonic> ChainMnemonics { get; }
        public List<NodeKey> FederationNodeKeys { get; private set; }
        public IList<FederationMemberKey> FederationMemberKeys { get; private set; }
        public IDictionary<FederationMemberKey, Mnemonic> FederationMembersMnemonics { get; private set; }
        public Script RedeemScript { get; private set; }
        public IDictionary<NodeKey, FederationWallet> FedWalletsByKey { get; }
        public IDictionary<NodeKey, CoreNode> NodesByKey { get; }

        private readonly NodeBuilder nodeBuilder;

        public readonly int FederationMemberCount;
        public int QuorumSize => FederationMemberCount / 2 + 1;

        public GatewayIntegrationTestEnvironment(NodeBuilder nodeBuilder, Network mainchainNetwork, Network sidechainNetwork, int federationMemberCount = 3)
        {
            FederationMembersMnemonics = new Dictionary<FederationMemberKey, Mnemonic>();
            ChainMnemonics = new Dictionary<Chain, Mnemonic>();
            NodesByKey = new Dictionary<NodeKey, CoreNode>();
            FedWalletsByKey = new Dictionary<NodeKey, FederationWallet>();
            Networks = new Dictionary<Chain, Network>
            {
                {Chain.Mainchain, mainchainNetwork},
                {Chain.Sidechain, sidechainNetwork }
            };

            this.nodeBuilder = nodeBuilder;
            FederationMemberCount = federationMemberCount;
            BuildFederationMembersNodeKeys();
            BuildMnemonics();
            BuildRedeemScript();
            BuildFederationNodes();
            BuildFederationWallets();
        }

        private void BuildFederationMembersNodeKeys()
        {
            FederationNodeKeys = new List<NodeKey>();
            foreach (var chain in Enum.GetValues(typeof(Chain)))
            {
                var keys = Enumerable.Range(0, FederationMemberCount)
                    .Select(i => new NodeKey { Chain = (Chain)chain, Role = NodeRole.Federation, Index = i }).ToList();
                FederationNodeKeys.AddRange(keys);
            }

            FederationMemberKeys = FederationNodeKeys.Select(n => n.AsFederationMemberKey()).Distinct().ToList();

            FederationNodeKeys.Count.Should().Be(FederationMemberCount * 2);
            FederationMemberKeys.Count.Should().Be(FederationMemberCount);
        }

        private void BuildMnemonics()
        {
            foreach (var federationMemberKey in FederationMemberKeys)
            {
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                FederationMembersMnemonics.Add(federationMemberKey, mnemonic);
            }
            //todo: make sure this is needed
            ChainMnemonics[Chain.Mainchain] = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ChainMnemonics[Chain.Sidechain] = new Mnemonic(Wordlist.English, WordCount.Twelve);
        }

        private void BuildRedeemScript()
        {
            this.RedeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(QuorumSize,
                FederationMembersMnemonics.Values
                    .Select(m => m.DeriveExtKey().PrivateKey.PubKey).ToArray());
        }

        private void BuildFederationNodes()
        {
            foreach (var key in FederationNodeKeys)
            {
                var addParametersAction = new Action<CoreNode>(n =>
                {
                    n.ConfigParameters.Add("apiport", key.SelfApiPort.ToString());
                    n.ConfigParameters.Add("counterchainapiport", key.CounterChainApiPort.ToString());
                    n.ConfigParameters.Add("redeemscript", this.RedeemScript.ToString());
                    n.ConfigParameters.Add("federationips",
                        string.Join(",", FederationNodeKeys
                            .Where(k => k.Chain == key.Chain && k.Index != key.Index)
                            .Select(k => $"127.0.0.1:{k.SelfApiPort}")
                        ));
                });
                TestHelper.BuildStartAndRegisterNode(nodeBuilder,
                    fullNodeBuilder => fullNodeBuilder
                        .UseBlockStore()
                        .UsePowConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddMining()
                        .AddFederationGateway()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC()
                        .MockIBD(),
                    key, NodesByKey, Networks[key.Chain], addParametersAction);
            }
        }

        private void BuildFederationWallets()
        {
            foreach (var key in FederationNodeKeys)
            {
                //todo: change that when FederationWallets are ready again
                var generalWalletManager = NodesByKey[key].FullNode.NodeService<IFederationWalletManager>();
                var federationWallet = generalWalletManager.GetWallet();
                FedWalletsByKey.Add(key, federationWallet);
            }
        }

        public BitcoinAddress GetMultisigAddress(Chain chain)
        {
            return RedeemScript.Hash.GetAddress(Networks[chain]);
        }

        public Script GetMultisigPubKey(Chain chain)
        {
            return GetMultisigAddress(chain).ScriptPubKey;
        }

        public void Dispose()
        {
            nodeBuilder?.Dispose();
        }
    }
}

