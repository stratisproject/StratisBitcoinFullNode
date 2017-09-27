using Stratis.Bitcoin.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using System.Threading.Tasks;
using Stratis.Bitcoin.Connection;
using System.Collections.Concurrent;
using ConcurrentCollections;
using System.Linq;

namespace Stratis.Bitcoin.Broadcasting
{
    public abstract class BroadcasterManagerBase : IBroadcasterManager
    {
        public event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        public void OnTransactionStateChanged(TransactionBroadcastEntry entry)
        {
            TransactionStateChanged?.Invoke(this, entry);
        }

        private ConcurrentHashSet<TransactionBroadcastEntry> Broadcasts { get; }

        public BroadcasterManagerBase()
        {
            this.Broadcasts = new ConcurrentHashSet<TransactionBroadcastEntry>();
        }

        public TransactionBroadcastEntry GetTransaction(uint256 transactionHash)
        {
            TransactionBroadcastEntry txEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transactionHash);
            if(txEntry == default(TransactionBroadcastEntry))
            {
                return null;
            }

            return txEntry;
        }

        public void AddOrUpdate(Transaction transaction, State state)
        {
            var newEntry = new TransactionBroadcastEntry(transaction, state);
            TransactionBroadcastEntry oldEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());
            if(oldEntry == default(TransactionBroadcastEntry))
            {
                this.Broadcasts.Add(newEntry);
                OnTransactionStateChanged(newEntry);
            }
            else
            {
                if(oldEntry.State != state)
                {
                    this.Broadcasts.TryRemove(oldEntry);
                    this.Broadcasts.Add(newEntry);
                    OnTransactionStateChanged(newEntry);
                }
            }
        }
        
        public abstract Task<Success> TryBroadcastAsync(Transaction transaction);
    }
}
