using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Event that is executed when a transaction is removed from the mempool.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class TransactionAddedToMemoryPool : EventBase
    {
        public Transaction AddedTransaction { get; }

        public TransactionAddedToMemoryPool(Transaction addedTransaction)
        {
            this.AddedTransaction = addedTransaction;
        }
    }
}