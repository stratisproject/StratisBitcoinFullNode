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
        Conservative,
        MemPoolMin,
        PayTxFee,
        Fallback,
        Required,
        MaxTxFee
    }
}
