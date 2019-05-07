using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;

namespace Stratis.Bitcoin.Features.Wallet.Events
{
    /// <summary>
    /// Event that is executed when a transaction is found in the wallet.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class TransactionFound : EventBase
    {
        public Transaction FoundTransaction { get; }

        public TransactionFound(Transaction foundTransaction)
        {
            this.FoundTransaction = foundTransaction;
        }
    }
}
