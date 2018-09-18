using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class TestMemPoolEntryHelper
    {
        // Default values
        private Money nFee = Money.Zero;

        private long nTime = 0;
        private double dPriority = 0.0;
        private int nHeight = 1;
        private bool spendsCoinbase = false;
        private long sigOpCost = 4;
        private LockPoints lp = new LockPoints();

        public TxMempoolEntry FromTx(Transaction tx, TxMempool pool = null)
        {
            Money inChainValue = (pool != null && pool.HasNoInputsOf(tx)) ? tx.TotalOut : 0;

            return new TxMempoolEntry(tx, this.nFee, this.nTime, this.dPriority, this.nHeight,
                inChainValue, this.spendsCoinbase, this.sigOpCost, this.lp, new ConsensusOptions());
        }

        // Change the default value
        public TestMemPoolEntryHelper Fee(Money fee) { this.nFee = fee; return this; }

        public TestMemPoolEntryHelper Time(long time)
        {
            this.nTime = time; return this;
        }

        public TestMemPoolEntryHelper Priority(double priority)
        {
            this.dPriority = priority; return this;
        }

        public TestMemPoolEntryHelper Height(int height)
        {
            this.nHeight = height; return this;
        }

        public TestMemPoolEntryHelper SpendsCoinbase(bool flag)
        {
            this.spendsCoinbase = flag; return this;
        }

        public TestMemPoolEntryHelper SigOpsCost(long sigopsCost)
        {
            this.sigOpCost = sigopsCost; return this;
        }
    }
}
