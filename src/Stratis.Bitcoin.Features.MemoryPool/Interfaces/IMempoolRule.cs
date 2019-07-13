namespace Stratis.Bitcoin.Features.MemoryPool.Interfaces
{
    /// <summary>
    /// Rule to be checked before a transaction is added to the mempool.
    /// </summary>
    public interface IMempoolRule
    {
        /// <summary>
        /// Check that the transaction meets certain criteria before being added to mempool.
        /// If it doesn't, a <see cref="ConsensusErrorException" /> will be thrown.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        void CheckTransaction(MempoolValidationContext context);
    }
}