using System;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public TransactionBroadcastState TransactionBroadcastState { get; set; }

        public string ErrorMessage => (this.MempoolError == null) ? string.Empty : (this.MempoolError.ConsensusError?.Message ?? this.MempoolError.Code ?? "Failed");

        public MempoolError MempoolError { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, TransactionBroadcastState transactionBroadcastState, MempoolError mempoolError)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.TransactionBroadcastState = transactionBroadcastState;
            this.MempoolError = mempoolError;
        }
    }
}
