using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Used to determine type of fee estimation requested
    /// </summary>
    public enum FeeEstimateMode
    {
        Unset,
        Economical,
        Conservative
    }
}
