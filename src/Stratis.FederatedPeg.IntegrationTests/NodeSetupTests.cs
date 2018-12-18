using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public void StartBothChainsWithWallets()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartAndConnectNodes();

                context.EnableSideFedWallets();
                context.EnableMainFedWallets();
            }
        }

        //[Fact]
        [Fact(Skip = "Unstable for a while. Requires fixing.")]
        public void FundMainChain()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartMainNodes();
                context.ConnectMainChainNodes();
                context.EnableMainFedWallets();

                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + (int)context.MainChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1, context.FedMain2, context.FedMain3);
                Assert.True(context.GetBalance(context.MainUser) > context.MainChainNetwork.Consensus.PremineReward);
            }
        }

        //[Fact]
        [Fact(Skip = "Requires fixing. this.FedMain1.FullNode is null after starting the nodes.")]
        public void FundSideChain()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartSideNodes();
                context.ConnectSideChainNodes();
                context.EnableSideFedWallets();

                // Wait for node to reach premine height
                TestHelper.WaitLoop(() => context.SideUser.FullNode.Chain.Height >= context.SideChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1, context.FedSide2, context.FedSide3);

                // Ensure that coinbase contains premine reward and it goes to the fed.
                Block block = context.SideUser.FullNode.Chain.GetBlock((int)context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);
            }
        }

        [Fact]
        public async Task MainChain_To_SideChain_Transfer()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                // Set everything up
                context.StartAndConnectNodes();
                context.EnableSideFedWallets();
                context.EnableMainFedWallets();

                // Fund a main chain node
                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + (int)context.MainChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1);
                Assert.True(context.GetBalance(context.MainUser) > context.MainChainNetwork.Consensus.PremineReward);

                // Let sidechain progress to point where fed has the premine
                TestHelper.WaitLoop(() => context.SideUser.FullNode.Chain.Height >= context.SideUser.FullNode.Network.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1);
                Block block = context.SideUser.FullNode.Chain.GetBlock((int)context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);

                // Send to sidechain
                string sidechainAddress = context.GetAddress(context.SideUser);
                await context.DepositToSideChain(context.MainUser, 25, sidechainAddress);
                TestHelper.WaitLoop(() => context.FedMain1.CreateRPCClient().GetRawMempool().Length == 1);
                TestHelper.MineBlocks(context.FedMain1, 15);

                // Sidechain user has balance
                Assert.Equal(new Money(25, MoneyUnit.BTC), context.GetBalance(context.SideUser));
            }
        }
    }
}
