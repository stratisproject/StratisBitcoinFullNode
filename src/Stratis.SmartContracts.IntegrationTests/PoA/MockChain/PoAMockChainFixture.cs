using System;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    public class PoAMockChainFixture : IDisposable
    {
        public PoAMockChain Chain { get; }

        public PoAMockChainFixture()
        {
            this.Chain = new PoAMockChain(2);
            var node1 = this.Chain.Nodes[0];
            var node2 = this.Chain.Nodes[1];
            var maturity = (int)this.Chain.Network.Consensus.CoinbaseMaturity;

            // Fund both nodes with the minimum to have something to spend.
            node1.MineBlocks(maturity + 1);
            node2.MineBlocks(maturity + 1);
        }

        public void Dispose()
        {
            this.Chain.Dispose();
        }
    }
}
