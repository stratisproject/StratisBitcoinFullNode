using System;
using System.Transactions;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public State State { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, State state)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.State = state;
        }
    }
}
