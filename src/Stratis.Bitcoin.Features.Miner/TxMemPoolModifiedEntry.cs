using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Container for tracking updates to ancestor feerate as we include (parent) transactions in a block.
    /// </summary>
    public sealed class TxMemPoolModifiedEntry
    {
        public readonly TxMempoolEntry MempoolEntry;

        public Money ModFeesWithAncestors;

        public long SigOpCostWithAncestors;

        public long SizeWithAncestors;

        public TxMemPoolModifiedEntry(TxMempoolEntry entry)
        {
            this.MempoolEntry = entry;
            this.ModFeesWithAncestors = entry.ModFeesWithAncestors;
            this.SigOpCostWithAncestors = entry.SigOpCostWithAncestors;
            this.SizeWithAncestors = entry.SizeWithAncestors;
        }
    }
}