using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using static Stratis.Bitcoin.Features.Miner.PowBlockDefinition;

namespace Stratis.Bitcoin.Features.Miner.Comparers
{
    /// <summary>
    /// This matches the calculation in CompareTxMemPoolEntryByAncestorFee,
    /// except operating on CTxMemPoolModifiedEntry.
    /// </summary>
    /// <remarks>TODO: Refactor to avoid duplication of this logic.</remarks>
    public sealed class CompareModifiedEntry : IComparer<TxMemPoolModifiedEntry>
    {
        public int Compare(TxMemPoolModifiedEntry a, TxMemPoolModifiedEntry b)
        {
            Money f1 = a.ModFeesWithAncestors * b.SizeWithAncestors;
            Money f2 = b.ModFeesWithAncestors * a.SizeWithAncestors;

            if (f1 == f2)
                return TxMempool.CompareIteratorByHash.InnerCompare(a.MempoolEntry, b.MempoolEntry);

            return f1 > f2 ? 1 : -1;
        }
    }
}