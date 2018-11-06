using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockReceiver
    {
        void ReceiveMaturedBlockDeposits(IMaturedBlockDeposits maturedBlockDeposits);

        IObservable<IMaturedBlockDeposits> MaturedBlockDepositStream { get; }
    }
}
