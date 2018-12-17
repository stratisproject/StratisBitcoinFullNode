using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class MaturedBlockReceiver : IMaturedBlockReceiver, IDisposable
    {
        private readonly ReplaySubject<IMaturedBlockDeposits[]> maturedBlockDepositStream;

        private readonly ILogger logger;

        public MaturedBlockReceiver(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockDepositStream = new ReplaySubject<IMaturedBlockDeposits[]>(1);
            this.MaturedBlockDepositStream = this.maturedBlockDepositStream.AsObservable();
        }

        /// <inheritdoc />
        public void PushMaturedBlockDeposits(IMaturedBlockDeposits[] maturedBlockDeposits)
        {
            if(maturedBlockDeposits == null) return;
            this.logger.LogDebug("Pushing {0} matured deposit(s)", maturedBlockDeposits.Length);
            this.logger.LogDebug("{0}", string.Join(Environment.NewLine, JsonConvert.SerializeObject(maturedBlockDeposits)));
            this.maturedBlockDepositStream.OnNext(maturedBlockDeposits);
        }

        /// <inheritdoc />
        public IObservable<IMaturedBlockDeposits[]> MaturedBlockDepositStream { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.maturedBlockDepositStream?.Dispose();
        }
    }
}