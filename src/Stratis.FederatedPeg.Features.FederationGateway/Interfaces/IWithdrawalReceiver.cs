using System;
using System.Collections.Generic;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IWithdrawalReceiver : IDisposable
    {
        IObservable<IReadOnlyList<IWithdrawal>> NewWithdrawalsOnTargetChainStream { get; }

        void ReceiveWithdrawals(IReadOnlyList<IWithdrawal> withdrawals);
    }
}