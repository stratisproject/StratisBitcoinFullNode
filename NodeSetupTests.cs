using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class NodeSetupTests
    {
        [Fact]
        public void NodeSetup()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartMainNodes();

                context.MainUser.State.Should().Be(CoreNodeState.Running);
                context.FedMain1.State.Should().Be(CoreNodeState.Running);
                context.FedMain2.State.Should().Be(CoreNodeState.Running);
                context.FedMain3.State.Should().Be(CoreNodeState.Running);

                context.StartSideNodes();

                context.SideUser.State.Should().Be(CoreNodeState.Running);
                context.FedSide1.State.Should().Be(CoreNodeState.Running);
                context.FedSide2.State.Should().Be(CoreNodeState.Running);
                context.FedSide3.State.Should().Be(CoreNodeState.Running);
            }
        }

        [Fact(Skip ="Sidechain nodes starting but can't execute endpoints when enabling wallets - make sure sidechains in TestBase are running as normal.")]
        public void EnableNodeWallets()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartAndConnectNodes();

                List<CoreNode> nodesToStart = new List<CoreNode>{ context.FedMain1, context.FedMain2, context.FedMain3, context.FedSide1, context.FedSide2, context.FedSide3};

                context.EnableWallets(nodesToStart);
            }
        }

        [Fact]
        public void FundMainChainNode()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartMainNodes();
                context.ConnectMainChainNodes();

                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + 1);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1, context.FedMain2, context.FedMain3);

                Assert.Equal(context.MainChainNetwork.Consensus.ProofOfWorkReward, context.GetBalance(context.MainUser));
            }
        }
        
        [Fact]
        public void Sidechain_Premine_Received()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartSideNodes();
                context.ConnectSideChainNodes();

                // Wait for node to reach premine height 
                TestHelper.WaitLoop(() => context.SideUser.FullNode.Chain.Height == context.SideUser.FullNode.Network.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1);

                // Ensure that coinbase contains premine reward and it goes to the fed.
                Block block = context.SideUser.FullNode.Chain.Tip.Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);
            }
        }
    }
}
