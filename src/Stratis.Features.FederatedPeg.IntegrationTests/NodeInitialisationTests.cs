using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.FederatedPeg.IntegrationTests.Utils;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.IntegrationTests
{
    /// <summary>
    /// These tests quickly help detect any issues with the DI or initialisation of the nodes.
    /// </summary>
    public class NodeInitialisationTests
    {
        private const int DepositConfirmations = 5;

        private readonly CirrusRegTest sidechainNetwork;
        private readonly Network mainNetwork;

        private readonly (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress
            mainchainMultisigAddress) scriptAndAddresses;


        public NodeInitialisationTests()
        {
            this.sidechainNetwork = (CirrusRegTest)CirrusNetwork.NetworksSelector.Regtest();
            this.mainNetwork = Networks.Stratis.Regtest();
            var pubKeysByMnemonic = this.sidechainNetwork.FederationMnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);
            this.scriptAndAddresses = FederatedPegTestHelper.GenerateScriptAndAddresses(this.mainNetwork, this.sidechainNetwork, 2, pubKeysByMnemonic);
        }

        [Fact]
        public void SidechainUserStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CoreNode user = nodeBuilder.CreateSidechainNode(this.sidechainNetwork);

                user.Start();

                Assert.Equal(CoreNodeState.Running, user.State);
            }
        }


        [Fact]
        public void SidechainMinerStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                Key federationKey = new Key();

                CoreNode miner = nodeBuilder.CreateSidechainMinerNode(this.sidechainNetwork, this.mainNetwork, federationKey);

                miner.Start();

                Assert.Equal(CoreNodeState.Running, miner.State);
            }
        }

        [Fact]
        public void SidechainGatewayStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                Key federationKey = new Key();

                CoreNode gateway = nodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.mainNetwork, federationKey);
                gateway.AppendToConfig("sidechain=1");
                gateway.AppendToConfig($"redeemscript={scriptAndAddresses.payToMultiSig.ToString()}");
                gateway.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                gateway.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders

                gateway.Start();

                Assert.Equal(CoreNodeState.Running, gateway.State);
            }
        }

        [Fact]
        public void MainChainGatewayStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CoreNode gateway = nodeBuilder.CreateMainChainFederationNode(this.mainNetwork, this.sidechainNetwork);
                gateway.AppendToConfig("mainchain=1");
                gateway.AppendToConfig($"redeemscript={scriptAndAddresses.payToMultiSig.ToString()}");
                gateway.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                gateway.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders

                gateway.Start();

                Assert.Equal(CoreNodeState.Running, gateway.State);
            }
        }

        [Fact]
        public void MinerPairStarts()
        {
            using (SidechainNodeBuilder sideNodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                Key federationKey = new Key();

                CoreNode side = sideNodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.mainNetwork, federationKey);
                side.AppendToConfig("sidechain=1");
                side.AppendToConfig($"redeemscript={scriptAndAddresses.payToMultiSig.ToString()}");
                side.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                side.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                side.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");

                CoreNode main = nodeBuilder.CreateStratisPosNode(this.mainNetwork);

                side.Start();
                main.Start();

                Assert.Equal(CoreNodeState.Running, main.State);
                Assert.Equal(CoreNodeState.Running, side.State);

                // TODO: Add collateral checks or some other proof they're talking
            }
        }

        [Fact]
        public async Task GatewayPairStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                Key federationKey = new Key();

                CoreNode side = nodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.mainNetwork, federationKey);
                side.AppendToConfig("sidechain=1");
                side.AppendToConfig($"redeemscript={scriptAndAddresses.payToMultiSig.ToString()}");
                side.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                side.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                side.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");

                CoreNode main = nodeBuilder.CreateMainChainFederationNode(this.mainNetwork, this.sidechainNetwork).WithWallet();
                main.AppendToConfig("mainchain=1");
                main.AppendToConfig($"redeemscript={scriptAndAddresses.payToMultiSig.ToString()}");
                main.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                main.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                main.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");

                side.Start();
                main.Start();

                Assert.Equal(CoreNodeState.Running, main.State);
                Assert.Equal(CoreNodeState.Running, side.State);

                $"http://localhost:{side.ApiPort}/api".AppendPathSegment("FederationWallet/enable-federation").PostJsonAsync(new EnableFederationRequest
                {
                    Password = "password",
                    Mnemonic = this.sidechainNetwork.FederationMnemonics[0].ToString()
                }).Result.StatusCode.Should().Be(HttpStatusCode.OK);

                $"http://localhost:{main.ApiPort}/api".AppendPathSegment("FederationWallet/enable-federation").PostJsonAsync(new EnableFederationRequest
                {
                    Password = "password",
                    Mnemonic = this.sidechainNetwork.FederationMnemonics[0].ToString()
                }).Result.StatusCode.Should().Be(HttpStatusCode.OK);

                // If one node progresses far enough that the other can advance it's NextMatureDepositHeight, they're talking!
                TestHelper.MineBlocks(main, DepositConfirmations + 1);
                TestBase.WaitLoop(() => side.FullNode.NodeService<ICrossChainTransferStore>().NextMatureDepositHeight > 0);
            }
        }

    }
}
