using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class WithdrawalReceiver : IWithdrawalReceiver, IDisposable
    {
        private readonly ReplaySubject<IReadOnlyList<IWithdrawal>> newWithdrawalsOnTargetChainStream;

        public WithdrawalReceiver()
        {
            this.newWithdrawalsOnTargetChainStream = new ReplaySubject<IReadOnlyList<IWithdrawal>>(1);
            this.NewWithdrawalsOnTargetChainStream = this.newWithdrawalsOnTargetChainStream.AsObservable();
        }

        /// <inheritdoc />
        public IObservable<IReadOnlyList<IWithdrawal>> NewWithdrawalsOnTargetChainStream { get; }

        /// <inheritdoc />
        public void ReceiveWithdrawals(IReadOnlyList<IWithdrawal> withdrawals)
        {
            this.newWithdrawalsOnTargetChainStream.OnNext(withdrawals);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.newWithdrawalsOnTargetChainStream?.Dispose();
        }
    }
}