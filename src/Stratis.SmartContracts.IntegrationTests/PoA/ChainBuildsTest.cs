using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoA.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoA
{
    public class ChainBuildsTest
    {
        [Fact]
        public void PoAMockChain_Node_Mines_And_Receives_Premine()
        {
            using (PoAMockChain chain = new PoAMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

                var tipBefore = node1.CoreNode.GetTip().Height;
                TestHelper.WaitLoop(() => node1.CoreNode.GetTip().Height >= tipBefore + 5);
                chain.WaitForAllNodesToSync();

                TestHelper.WaitLoop(() => node1.CoreNode.GetTip().Height >= chain.Network.Consensus.PremineHeight + chain.Network.Consensus.CoinbaseMaturity + 1);
                Assert.Equal(chain.Network.Consensus.PremineReward.Satoshi, (long) node1.WalletSpendableBalance);
            }
        }

    }
}
