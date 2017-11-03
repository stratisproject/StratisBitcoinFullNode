using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public Transaction Transaction { get; }
        public State State { get; }
        public TransactionBroadcastEntry(Transaction transaction, State state)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.State = state;
        }
    }
}