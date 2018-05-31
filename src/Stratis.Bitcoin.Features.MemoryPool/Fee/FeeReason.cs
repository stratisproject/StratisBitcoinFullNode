using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Enumeration of reason for returned fee estimate
    /// </summary>
    public enum FeeReason
    {
        None,
        HalfEstimate,
        FullEstimate,
        DoubleEstimate,
        Coservative,
        MemPoolMin,
        PayTxFee,
        Fallback,
        Required,
        MaxTxFee
    }
}
