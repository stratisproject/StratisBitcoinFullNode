﻿using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Event that is executed when a transaction is removed from the mempool.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class TxRemoved : EventBase
    {
        public Transaction RemovedTransaction { get; }

        public TxRemoved(Transaction removedTransaction)
        {
            this.RemovedTransaction = removedTransaction;
        }
    }
}
