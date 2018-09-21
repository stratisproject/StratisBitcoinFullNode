using System;

namespace Stratis.SmartContracts.IntegrationTests.MockChain
{
    public class MockChainFixture : IDisposable
    {
        public Chain Chain { get; }

        public MockChainFixture()
        {
            this.Chain = new Chain(2);
            var sender = this.Chain.Nodes[0];
            var receiver = this.Chain.Nodes[1];
            var maturity = (int)this.Chain.Network.Consensus.CoinbaseMaturity;
            sender.MineBlocks(maturity + 1);
        }

        public void Dispose()
        {
            this.Chain.Dispose();
        }
    }
}
