using System;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Container for tracking updates to ancestor feerate as we include (parent) transactions in a block.
    /// </summary>
    public sealed class TxMemPoolModifiedEntry:IComparable, ITxMempoolFees
    {
        public readonly TxMempoolEntry MempoolEntry;

        /// <summary>
        /// Gets the size of the transaction with it's ancestors.</summary>
        public long SizeWithAncestors { get; set; }

        /// <summary>Gets the total fees of the transaction including it's ancestors.</summary>
        public Money ModFeesWithAncestors { get; set; }

        public long SigOpCostWithAncestors;

        public TxMemPoolModifiedEntry(TxMempoolEntry entry)
        {
            this.MempoolEntry = entry;
            this.ModFeesWithAncestors = entry.ModFeesWithAncestors;
            this.SigOpCostWithAncestors = entry.SigOpCostWithAncestors;
            this.SizeWithAncestors = entry.SizeWithAncestors;
        }

        /// <summary>
        /// Default comparator for comparing this object to another TxMemPoolModifiedEntry object.
        /// </summary>
        /// <param name="other">Modified memory pool entry to compare to.</param>
        /// <returns>Result of comparison function.</returns>
        public int CompareTo(object other)
        {
            return uint256.Comparison(this.MempoolEntry.TransactionHash, (other as TxMemPoolModifiedEntry).MempoolEntry.TransactionHash);
        }
    }
}