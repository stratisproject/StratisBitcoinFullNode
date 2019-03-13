using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Sidechains.Networks;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainTestContext : IDisposable
    {
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccount = "account 0";
        private const string ConfigSideChain = "sidechain";
        private const string ConfigMainChain = "mainchain";
        private const string ConfigAgentPrefix = "agentprefix";

        // TODO: Make these private, or move to public immutable properties. Will happen naturally over time.

        public readonly IList<Mnemonic> mnemonics;
        public readonly Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;
        public readonly (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;
        public readonly List<string> chains;

        private readonly SidechainNodeBuilder nodeBuilder;

        public Network MainChainNetwork { get; }

        public FederatedPegRegTest SideChainNetwork { get; }

        // TODO: HashSets / Readonly
        public IReadOnlyList<CoreNode> MainChainNodes { get; }
        public IReadOnlyList<CoreNode> SideChainNodes { get; }
        public IReadOnlyList<CoreNode> MainChainFedNodes { get; }
        public IReadOnlyList<CoreNode> SideChainFedNodes { get; }

        public CoreNode MainUser{ get; }
        public CoreNode FedMain1 { get; }
        public CoreNode FedMain2 { get; }
        public CoreNode FedMain3 { get; }

        public CoreNode SideUser { get; }
        public CoreNode FedSide1 { get; }
        public CoreNode FedSide2 { get; }
        public CoreNode FedSide3 { get; }

        public SidechainTestContext()
        {
            this.MainChainNetwork = Networks.Stratis.Regtest();
            this.SideChainNetwork = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            this.mnemonics = this.SideChainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = FederatedPegTestHelper.GenerateScriptAndAddresses(this.MainChainNetwork, this.SideChainNetwork, 2, this.pubKeysByMnemonic);

            this.chains = new[] { "mainchain", "sidechain" }.ToList();

            this.nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this);

            this.MainUser = this.nodeBuilder.CreateStratisPosNode(this.MainChainNetwork, nameof(this.MainUser)).WithWallet();
            this.FedMain1 = this.nodeBuilder.CreateMainChainFederationNode(this.MainChainNetwork).WithWallet();
            this.FedMain2 = this.nodeBuilder.CreateMainChainFederationNode(this.MainChainNetwork);
            this.FedMain3 = this.nodeBuilder.CreateMainChainFederationNode(this.MainChainNetwork);

            this.SideUser = this.nodeBuilder.CreateSidechainNode(this.SideChainNetwork).WithWallet();

            this.FedSide1 = this.nodeBuilder.CreateSidechainFederationNode(this.SideChainNetwork, this.SideChainNetwork.FederationKeys[0]);
            this.FedSide2 = this.nodeBuilder.CreateSidechainFederationNode(this.SideChainNetwork, this.SideChainNetwork.FederationKeys[1]);
            this.FedSide3 = this.nodeBuilder.CreateSidechainFederationNode(this.SideChainNetwork, this.SideChainNetwork.FederationKeys[2]);

            this.SideChainNodes = new List<CoreNode>()
            {
                this.SideUser,
                this.FedSide1,
                this.FedSide2,
                this.FedSide3
            };

            this.MainChainNodes = new List<CoreNode>()
            {
                this.MainUser,
                this.FedMain1,
                this.FedMain2,
                this.FedMain3,
            };

            this.SideChainFedNodes = new List<CoreNode>()
            {
                this.FedSide1,
                this.FedSide2,
                this.FedSide3
            };

            this.MainChainFedNodes = new List<CoreNode>()
            {
                this.FedMain1,
                this.FedMain2,
                this.FedMain3
            };

            this.ApplyConfigParametersToNodes();
        }

        public void StartAndConnectNodes()
        {
            this.StartMainNodes();
            this.StartSideNodes();

            TestHelper.WaitLoop(() => this.FedMain3.State == CoreNodeState.Running && this.FedSide3.State == CoreNodeState.Running);

            this.ConnectMainChainNodes();
            this.ConnectSideChainNodes();
        }

        public void StartMainNodes()
        {
            foreach (CoreNode node in this.MainChainNodes)
            {
                node.Start();
            }
        }

        public void StartSideNodes()
        {
            foreach (CoreNode node in this.SideChainNodes)
            {
                node.Start();
            }
        }

        public void ConnectMainChainNodes()
        {
            TestHelper.Connect(this.MainUser, this.FedMain1);
            TestHelper.Connect(this.MainUser, this.FedMain2);
            TestHelper.Connect(this.MainUser, this.FedMain3);

            TestHelper.Connect(this.FedMain1, this.MainUser);
            TestHelper.Connect(this.FedMain1, this.FedMain2);
            TestHelper.Connect(this.FedMain1, this.FedMain3);

            TestHelper.Connect(this.FedMain2, this.MainUser);
            TestHelper.Connect(this.FedMain2, this.FedMain1);
            TestHelper.Connect(this.FedMain2, this.FedMain3);

            TestHelper.Connect(this.FedMain3, this.MainUser);
            TestHelper.Connect(this.FedMain3, this.FedMain1);
            TestHelper.Connect(this.FedMain3, this.FedMain2);
        }

        public void ConnectSideChainNodes()
        {
            TestHelper.Connect(this.SideUser, this.FedSide1);
            TestHelper.Connect(this.SideUser, this.FedSide2);
            TestHelper.Connect(this.SideUser, this.FedSide3);

            TestHelper.Connect(this.FedSide1, this.SideUser);
            TestHelper.Connect(this.FedSide1, this.FedSide2);
            TestHelper.Connect(this.FedSide1, this.FedSide3);

            TestHelper.Connect(this.FedSide2, this.SideUser);
            TestHelper.Connect(this.FedSide2, this.FedSide1);
            TestHelper.Connect(this.FedSide2, this.FedSide3);

            TestHelper.Connect(this.FedSide3, this.SideUser);
            TestHelper.Connect(this.FedSide3, this.FedSide1);
            TestHelper.Connect(this.FedSide3, this.FedSide2);
        }

        public void EnableMainFedWallets()
        {
            EnableFederationWallets(this.MainChainFedNodes);
        }

        public void EnableSideFedWallets()
        {
            EnableFederationWallets(this.SideChainFedNodes);
        }

        private void EnableFederationWallets(IReadOnlyList<CoreNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                CoreNode node = nodes[i];

                $"http://localhost:{node.ApiPort}/api".AppendPathSegment("FederationWallet/enable-federation").PostJsonAsync(new EnableFederationRequest
                {
                    Password = "password",
                    Mnemonic = this.mnemonics[i].ToString()
                }).Result.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// Helper method to build and send a deposit transaction to the federation on the main chain.
        /// </summary>
        public async Task DepositToSideChain(CoreNode node, decimal amount, string sidechainDepositAddress)
        {
            HttpResponseMessage depositTransaction = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new
                {
                    walletName = WalletName,
                    accountName = WalletAccount,
                    password =  WalletPassword,
                    opReturnData = sidechainDepositAddress,
                    feeAmount = "0.01",
                    recipients = new[]
                    {
                        new
                        {
                            destinationAddress = this.scriptAndAddresses.mainchainMultisigAddress.ToString(),
                            amount = amount
                        }
                    }
                });

            string result = await depositTransaction.Content.ReadAsStringAsync();
            WalletBuildTransactionModel walletBuildTxModel = JsonConvert.DeserializeObject<WalletBuildTransactionModel>(result);

            HttpResponseMessage sendTransactionResponse = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/send-transaction")
                .PostJsonAsync(new
                {
                    hex = walletBuildTxModel.Hex
                });
        }

        public async Task WithdrawToMainChain(CoreNode node, decimal amount, string mainchainWithdrawAddress)
        {
            HttpResponseMessage withdrawTransaction = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/build-transaction")
                .PostJsonAsync(new
                {
                    walletName = WalletName,
                    accountName = WalletAccount,
                    password = WalletPassword,
                    opReturnData = mainchainWithdrawAddress,
                    feeAmount = "0.01",
                    recipients = new[]
                    {
                        new
                        {
                            destinationAddress = this.scriptAndAddresses.sidechainMultisigAddress.ToString(),
                            amount = amount
                        }
                    }
                });

            string result = await withdrawTransaction.Content.ReadAsStringAsync();
            WalletBuildTransactionModel walletBuildTxModel = JsonConvert.DeserializeObject<WalletBuildTransactionModel>(result);

            HttpResponseMessage sendTransactionResponse = await $"http://localhost:{node.ApiPort}/api"
                .AppendPathSegment("wallet/send-transaction")
                .PostJsonAsync(new
                {
                    hex = walletBuildTxModel.Hex
                });
        }

        private void ApplyFederationIPs(CoreNode fed1, CoreNode fed2, CoreNode fed3)
        {
            string fedIps = $"{fed1.Endpoint},{fed2.Endpoint},{fed3.Endpoint}";

            fed1.AppendToConfig($"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            fed2.AppendToConfig($"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
            fed3.AppendToConfig($"{FederationGatewaySettings.FederationIpsParam}={fedIps}");
        }

        private void ApplyCounterChainAPIPort(CoreNode fromNode, CoreNode toNode)
        {
            fromNode.AppendToConfig($"{FederationGatewaySettings.CounterChainApiPortParam}={toNode.ApiPort.ToString()}");
            toNode.AppendToConfig($"{FederationGatewaySettings.CounterChainApiPortParam}={fromNode.ApiPort.ToString()}");
        }

        private void ApplyConfigParametersToNodes()
        {
            this.FedSide1.AppendToConfig($"{ConfigSideChain}=1");
            this.FedSide2.AppendToConfig($"{ConfigSideChain}=1");
            this.FedSide3.AppendToConfig($"{ConfigSideChain}=1");

            this.FedMain1.AppendToConfig($"{ConfigMainChain}=1");
            this.FedMain2.AppendToConfig($"{ConfigMainChain}=1");
            this.FedMain3.AppendToConfig($"{ConfigMainChain}=1");

            this.FedSide1.AppendToConfig($"mindepositconfirmations=5");
            this.FedSide2.AppendToConfig($"mindepositconfirmations=5");
            this.FedSide3.AppendToConfig($"mindepositconfirmations=5");

            this.FedMain1.AppendToConfig($"mindepositconfirmations=5");
            this.FedMain2.AppendToConfig($"mindepositconfirmations=5");
            this.FedMain3.AppendToConfig($"mindepositconfirmations=5");

            this.FedSide1.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.FedSide2.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.FedSide3.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");

            this.FedMain1.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.FedMain2.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");
            this.FedMain3.AppendToConfig($"{FederationGatewaySettings.RedeemScriptParam}={this.scriptAndAddresses.payToMultiSig.ToString()}");

            this.FedSide1.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[0]].ToString()}");
            this.FedSide2.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[1]].ToString()}");
            this.FedSide3.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[2]].ToString()}");

            this.FedMain1.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[0]].ToString()}");
            this.FedMain2.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[1]].ToString()}");
            this.FedMain3.AppendToConfig($"{FederationGatewaySettings.PublicKeyParam}={this.pubKeysByMnemonic[this.mnemonics[2]].ToString()}");

            this.ApplyFederationIPs(this.FedMain1, this.FedMain2, this.FedMain3);
            this.ApplyFederationIPs(this.FedSide1, this.FedSide2, this.FedSide3);

            this.ApplyCounterChainAPIPort(this.FedMain1, this.FedSide1);
            this.ApplyCounterChainAPIPort(this.FedMain2, this.FedSide2);
            this.ApplyCounterChainAPIPort(this.FedMain3, this.FedSide3);

            this.ApplyAgentPrefixToNodes();
        }

        private void ApplyAgentPrefixToNodes()
        {
            // name assigning a little gross here - fix later.
            string[] names = new string[] {"SideUser", "FedSide1", "FedSide2", "FedSide3", "MainUser", "FedMain1", "FedMain2", "FedMain3"};
            int index = 0;
            foreach (CoreNode n in this.SideChainNodes.Concat(this.MainChainNodes))
            {
                string text = File.ReadAllText(n.Config);
                text = text.Replace($"{ConfigAgentPrefix}=node{n.Endpoint.Port}", $"{ConfigAgentPrefix}={names[index]}");
                File.WriteAllText(n.Config, text);
                index++;
            }
        }

        public void Dispose()
        {
            this.nodeBuilder?.Dispose();
        }
    }
}
