using System;
using NBitcoin;

namespace Stratis.Bitcoin.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public Transaction Transaction { get; }

        public State State { get; set; }

        public string ErrorMessage { get; set; }

        public TransactionBroadcastEntry(Transaction transaction, State state, string errorMessage)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.State = state;
            this.ErrorMessage = errorMessage;
        }
    }
}
