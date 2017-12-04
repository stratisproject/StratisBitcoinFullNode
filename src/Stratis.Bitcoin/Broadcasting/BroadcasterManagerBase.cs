using System;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Broadcasting
{
    public abstract class BroadcasterManagerBase : IBroadcasterManager
    {
        public event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        /// <summary>Connection manager for managing node connections.</summary>
        protected readonly IConnectionManager connectionManager;

        public BroadcasterManagerBase(IConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
            this.Broadcasts = new ConcurrentHashSet<TransactionBroadcastEntry>();
        }

        public void OnTransactionStateChanged(TransactionBroadcastEntry entry)
        {
            this.TransactionStateChanged?.Invoke(this, entry);
        }

        private ConcurrentHashSet<TransactionBroadcastEntry> Broadcasts { get; }

        public TransactionBroadcastEntry GetTransaction(uint256 transactionHash)
        {
            TransactionBroadcastEntry txEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transactionHash);
            if (txEntry == default(TransactionBroadcastEntry))
                return null;

            return txEntry;
        }

        public void AddOrUpdate(Transaction transaction, State state)
        {
            var newEntry = new TransactionBroadcastEntry(transaction, state);
            TransactionBroadcastEntry oldEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());
            if (oldEntry == default(TransactionBroadcastEntry))
            {
                this.Broadcasts.Add(newEntry);
                this.OnTransactionStateChanged(newEntry);
            }
            else
            {
                if (oldEntry.State != state)
                {
                    this.Broadcasts.TryRemove(oldEntry);
                    this.Broadcasts.Add(newEntry);
                    this.OnTransactionStateChanged(newEntry);
                }
            }
        }

        public abstract Task<Success> TryBroadcastAsync(Transaction transaction);
    }
}
