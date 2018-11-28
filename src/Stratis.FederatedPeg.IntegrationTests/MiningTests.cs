using System;
using System.Linq;

using NBitcoin;

using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Sidechains.Networks;

using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class MiningTests
    {
        [Fact]
        public void NodeCanLoadFederationKey()
        {
            var network = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                // Create first node as fed member.
                Key key = network.FederationKeys[0];
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

        [Fact(Skip = "I can't make it work yet.")]
        public void NodeCanMine()
        {
            var network = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                CoreNode node0 = builder.CreatePoANode(network, network.FederationKeys[0]).Start();
                CoreNode node1 = builder.CreatePoANode(network, network.FederationKeys[1]).Start();
                node0.EnableFastMining();
                node1.EnableFastMining();

                var tipBefore = node0.GetTip().Height;
                TestHelper.WaitLoop(
                    () =>
                        {
                            return node0.GetTip().Height >= tipBefore + 5;
                        }
                    );
            }
        }

        [Fact(Skip = "I can't make it work yet.")]
        public void PremineIsReceived()
        {
            var network = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                string walletName = "mywallet";
                CoreNode node = builder.CreatePoANode(network, network.FederationKeys[0]).WithWallet("pass", walletName).Start();
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
