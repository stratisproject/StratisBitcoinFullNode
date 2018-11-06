using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class MaturedBlockReceiver : IMaturedBlockReceiver, IDisposable
    {
        private readonly ReplaySubject<IMaturedBlockDeposits> maturedBlockDepositStream;

        private readonly ILogger logger;

        public MaturedBlockReceiver(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockDepositStream = new ReplaySubject<IMaturedBlockDeposits>(1);
            this.MaturedBlockDepositStream = this.maturedBlockDepositStream.AsObservable();
        }

        /// <inheritdoc />
        public void ReceiveMaturedBlockDeposits(IMaturedBlockDeposits maturedBlockDeposits)
        {
            this.logger.LogDebug("Received new matured block for{0}{1}", Environment.NewLine, this.maturedBlockDepositStream);
            this.maturedBlockDepositStream.OnNext(maturedBlockDeposits);
        }

        /// <inheritdoc />
        public IObservable<IMaturedBlockDeposits> MaturedBlockDepositStream { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.maturedBlockDepositStream?.Dispose();
        }
    }
}