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
        Task<Success> TryBroadcast(Transaction transaction);
        Success IsPropagated(Transaction transaction);
        Task<Success> WaitPropagation(Transaction transaction, TimeSpan timeout);
        event EventHandler<Transaction> OnTransactionPropagation;
    }
}
