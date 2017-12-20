using System.Collections.Generic;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class UnspentOutputsComparer : IComparer<UnspentOutputs>
    {
        public static UnspentOutputsComparer Instance { get; } = new UnspentOutputsComparer();

        private readonly UInt256Comparer Comparer = new UInt256Comparer();

        public int Compare(UnspentOutputs x, UnspentOutputs y)
        {
            return this.Comparer.Compare(x.TransactionId, y.TransactionId);
        }
    }
}
