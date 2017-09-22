using NBitcoin;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Broadcasting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBroadcasterManager
    {
        Task<Success> TryBroadcastAsync(Transaction transaction);
        event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;
        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);
        void AddOrUpdate(Transaction transaction, State state);
    }
}
