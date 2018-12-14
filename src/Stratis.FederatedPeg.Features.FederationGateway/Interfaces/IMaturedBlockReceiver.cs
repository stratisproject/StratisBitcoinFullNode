using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockReceiver
    {
        void PushMaturedBlockDeposits(IMaturedBlockDeposits[] maturedBlockDeposits);

        IObservable<IMaturedBlockDeposits[]> MaturedBlockDepositStream { get; }
    }
}
