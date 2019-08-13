using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.IntegrationTests;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.Collateral;
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

                VerifyNodeComposition(user);
            }
        }

        [Fact]
        public void SidechainMinerStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                Key federationKey = new Key();

                CoreNode miner = nodeBuilder.CreateSidechainMinerNode(this.sidechainNetwork, this.mainNetwork, federationKey);

                this.StartNodeWithMockCounterNodeAPI(miner);

                Assert.Equal(CoreNodeState.Running, miner.State);

                VerifyNodeComposition(miner);
            }
        }

        private void StartNodeWithMockCounterNodeAPI(CoreNode node)
        {
            var mockClient = new Mock<IBlockStoreClient>();
            mockClient.Setup(x => x.GetVerboseAddressesBalancesDataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Bitcoin.Controllers.Models.VerboseAddressBalancesResult(100000));

            node.Start(() =>
            {
                ICollateralChecker collateralChecker = node.FullNode.NodeService<ICollateralChecker>();
                collateralChecker.SetPrivateVariableValue("blockStoreClient", mockClient.Object);
            });
        }

        [Fact]
        public void SidechainGatewayStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                Key federationKey = new Key();

                CoreNode gateway = nodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.mainNetwork, federationKey);
                gateway.AppendToConfig("sidechain=1");
                gateway.AppendToConfig($"redeemscript={this.scriptAndAddresses.payToMultiSig}");
                gateway.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                gateway.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders

                this.StartNodeWithMockCounterNodeAPI(gateway);

                Assert.Equal(CoreNodeState.Running, gateway.State);

                VerifyNodeComposition(gateway);
            }
        }

        [Fact]
        public void MainChainGatewayStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CoreNode gateway = nodeBuilder.CreateMainChainFederationNode(this.mainNetwork, this.sidechainNetwork);
                gateway.AppendToConfig("mainchain=1");
                gateway.AppendToConfig($"redeemscript={this.scriptAndAddresses.payToMultiSig.ToString()}");
                gateway.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                gateway.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders

                gateway.Start();

                Assert.Equal(CoreNodeState.Running, gateway.State);

                VerifyNodeComposition(gateway);
            }
        }

        [Fact]
        public void MinerPairStarts()
        {
            CirrusRegTest collateralSidechainNetwork = new CirrusSingleCollateralRegTest();

            using (SidechainNodeBuilder sideNodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                CoreNode main = nodeBuilder.CreateStratisPosNode(this.mainNetwork).WithWallet();
                main.AppendToConfig("addressindex=1");

                Key federationKey = new Key();

                CoreNode side = sideNodeBuilder.CreateSidechainMinerNode(collateralSidechainNetwork, this.mainNetwork, federationKey);
                side.AppendToConfig("sidechain=1");
                side.AppendToConfig($"redeemscript={this.scriptAndAddresses.payToMultiSig}");
                side.AppendToConfig($"publickey={collateralSidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                side.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                side.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");
                side.AppendToConfig($"counterchainapiport={main.ApiPort}");

                main.Start();
                side.Start();

                Assert.Equal(CoreNodeState.Running, main.State);
                Assert.Equal(CoreNodeState.Running, side.State);

                // Collateral is checked - they're talking!
                TestHelper.MineBlocks(main, 1);
                TestBase.WaitLoop(() => side.FullNode.NodeService<ICollateralChecker>().GetCounterChainConsensusHeight() > 0);
            }
        }

        [Fact]
        public void GatewayPairStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CoreNode side = nodeBuilder.CreateSidechainFederationNode(this.sidechainNetwork, this.mainNetwork, this.sidechainNetwork.FederationKeys[0]);
                side.AppendToConfig("sidechain=1");
                side.AppendToConfig($"redeemscript={this.scriptAndAddresses.payToMultiSig}");
                side.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                side.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                side.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");

                CoreNode main = nodeBuilder.CreateMainChainFederationNode(this.mainNetwork, this.sidechainNetwork).WithWallet();
                main.AppendToConfig("mainchain=1");
                main.AppendToConfig($"redeemscript={this.scriptAndAddresses.payToMultiSig}");
                main.AppendToConfig($"publickey={this.sidechainNetwork.FederationMnemonics[0].DeriveExtKey().PrivateKey.PubKey}");
                main.AppendToConfig("federationips=0.0.0.0,0.0.0.1"); // Placeholders
                main.AppendToConfig($"mindepositconfirmations={DepositConfirmations}");

                side.AppendToConfig($"counterchainapiport={main.ApiPort}");
                main.AppendToConfig($"counterchainapiport={side.ApiPort}");

                main.Start();
                side.Start();

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

                // TODO: Possible configuration issue with PoA here. Checking for commitment in case where there is no collateral required.
                //await side.MineBlocksAsync(DepositConfirmations + 1);
                //TestBase.WaitLoop(() => main.FullNode.NodeService<ICrossChainTransferStore>().NextMatureDepositHeight > 0);
            }
        }

        /// <summary>
        /// Verifies that the created node has certain properties.
        /// </summary>
        private static void VerifyNodeComposition(CoreNode node)
        {
            // TODO: Add more checks about the sanctity of the node. And add specific checks per particular daemon.

            // We only want one consensus rule engine. Others can sneak in and will break the periodic log.
            IEnumerable<IConsensusRuleEngine> consensusRuleEngines = node.FullNode.NodeService<IEnumerable<IConsensusRuleEngine>>();
            Assert.Single(consensusRuleEngines);
        }
    }

    public class CirrusSingleCollateralRegTest : CirrusRegTest
    {
        public CirrusSingleCollateralRegTest()
        {
            this.Name = "CirrusSingleCollateralRegTest";
            CollateralFederationMember firstMember = this.ConsensusOptions.GenesisFederationMembers[0] as CollateralFederationMember;
            firstMember.CollateralAmount = Money.Coins(100m);
            firstMember.CollateralMainchainAddress = new Key().ScriptPubKey.GetDestinationAddress(this).ToString();
        }
    }
}
