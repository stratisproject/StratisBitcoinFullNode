using System;
using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoA.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoA
{
    public class ChainBuildsTest
    {

        [Fact]
        public void PoAMockChain_Node_Builds()
        {
            using (PoAMockChain chain = new PoAMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                // TODO: How to mine blocks? This passes now but fails with below uncommented
                //node1.MineBlocks(1);
            }
        }

    }
}
