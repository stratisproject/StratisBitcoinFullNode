using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class UInt256Comparer : IComparer<uint256>
    {
        public int Compare(uint256 x, uint256 y)
        {
            if (x < y) return -1;
            if (x > y) return 1;
            return 0;
        }
    }
}
