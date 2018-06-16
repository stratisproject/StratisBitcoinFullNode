using System.Collections.Generic;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Miner.Comparers
{
    /// <summary>
    /// A comparator that sorts transactions based on number of ancestors.
    /// <para>
    /// This is sufficient to sort an ancestor package in an order that is valid
    /// to appear in a block.
    /// </para>
    /// </summary>
    public sealed class CompareTxIterByAncestorCount : IComparer<TxMempoolEntry>
    {
        public int Compare(TxMempoolEntry a, TxMempoolEntry b)
        {
            if (a.CountWithAncestors != b.CountWithAncestors)
                return a.CountWithAncestors < b.CountWithAncestors ? -1 : 1;

            return a.CompareTo(b);
        }
    }
}