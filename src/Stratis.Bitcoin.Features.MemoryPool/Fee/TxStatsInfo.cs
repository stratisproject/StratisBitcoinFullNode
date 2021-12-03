namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Transaction statistics information.
    /// </summary>
    public class TxStatsInfo
    {
        /// <summary>The block height.</summary>
        public int blockHeight;

        /// <summary>The index into the confirmed transactions bucket map.</summary>
        public int bucketIndex;

        /// <summary>
        /// Constructs an instance of a transaction stats info object.
        /// </summary>
        public TxStatsInfo()
        {
            this.blockHeight = 0;
            this.bucketIndex = 0;
        }
    }
}
