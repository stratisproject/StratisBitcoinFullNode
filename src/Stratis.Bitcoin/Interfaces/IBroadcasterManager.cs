using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;

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