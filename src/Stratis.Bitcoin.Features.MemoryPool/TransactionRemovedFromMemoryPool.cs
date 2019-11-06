using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Event that is executed when a transaction is removed from the mempool.
    /// </summary>
    /// <seealso cref="EventBase" />
    public class TransactionRemovedFromMemoryPool : EventBase
    {
        public Transaction RemovedTransaction { get; }

        /// <summary>
        /// Whether this transaction was removed from the mempool because
        /// it was included in a block that was added to the chain.
        /// </summary>
        /// <remarks>
        /// This is useful to discern whether we expect to see a transaction again.
        ///
        /// In the case that this is true, we can expect the transaction hash to still
        /// be useful, as it has been or will soon be "confirmed" and exist as part of the chain.
        ///
        /// In the case that this is false, we are most likely to not see the transaction hash again.
        /// It was likely removed because of an input conflict or a similar error, so we may
        /// want to discard the transaction hash entirely.
        /// </remarks>
        public bool RemovedForBlock { get; }

        public TransactionRemovedFromMemoryPool(Transaction removedTransaction, bool removedForBlock)
        {
            this.RemovedTransaction = removedTransaction;
            this.RemovedForBlock = removedForBlock;
        }
    }
}
