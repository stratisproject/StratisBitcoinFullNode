using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class MiningTests
    {
        [Fact]
        public void NodeCanLoadFederationKey()
        {
            var network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                // Create first node as fed member.
                Key key = network.FederationKey1;
                CoreNode node = builder.CreatePoANode(network, key).Start();

                Assert.True(node.FullNode.NodeService<FederationManager>().IsFederationMember);
                Assert.Equal(node.FullNode.NodeService<FederationManager>().FederationMemberKey, key);
                Assert.True(node.FullNode.NodeService<IPoAMiner>().IsMining());

                // Create second node as normal node.
                CoreNode node2 = builder.CreatePoANode(network).Start();

                Assert.False(node2.FullNode.NodeService<FederationManager>().IsFederationMember);
                Assert.Equal(node2.FullNode.NodeService<FederationManager>().FederationMemberKey, null);
                Assert.False(node2.FullNode.NodeService<IPoAMiner>().IsMining());
            }
        }

        [Fact]
        public void NodeCanMine()
        {
            var network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                CoreNode node = builder.CreatePoANode(network, network.FederationKey1).Start();
                node.EnableFastMining();

                var tipBefore = node.GetTip().Height;
                TestHelper.WaitLoop(() => node.GetTip().Height >= tipBefore + 5);
            }
        }

        [Fact]
        public void PremineIsReceived()
        {
            TestPoANetwork network = new TestPoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                string walletName = "mywallet";
                CoreNode node = builder.CreatePoANode(network, network.FederationKey1).WithWallet("pass", walletName).Start();
                node.EnableFastMining();

                IWalletManager walletManager = node.FullNode.NodeService<IWalletManager>();
                long balanceOnStart = walletManager.GetBalances(walletName, "account 0").Sum(x => x.AmountConfirmed);
                Assert.Equal(0, balanceOnStart);

                TestHelper.WaitLoop(() => node.GetTip().Height >= network.Consensus.PremineHeight + network.Consensus.CoinbaseMaturity + 1);

                long balanceAfterPremine = walletManager.GetBalances(walletName, "account 0").Sum(x => x.AmountConfirmed);

                Assert.Equal(network.Consensus.PremineReward.Satoshi, balanceAfterPremine);
            }
        }
    }
}
