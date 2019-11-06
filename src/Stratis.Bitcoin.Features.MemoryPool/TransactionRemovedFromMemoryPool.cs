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

        public bool RemovedForBlock { get; }

        public TransactionRemovedFromMemoryPool(Transaction removedTransaction, bool removedForBlock)
        {
            this.RemovedTransaction = removedTransaction;
            this.RemovedForBlock = removedForBlock;
        }
    }
}
