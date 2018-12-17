using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public interface IMaturedBlockReceiver
    {
        void PushMaturedBlockDeposits(IMaturedBlockDeposits[] maturedBlockDeposits);

        IObservable<IMaturedBlockDeposits[]> OnMaturedBlockDepositsPushed { get; }
    }

    public class MaturedBlockReceiver : IMaturedBlockReceiver, IDisposable
    {
        private readonly ReplaySubject<IMaturedBlockDeposits[]> maturedBlockDepositStream;

        private readonly ILogger logger;

        /// <inheritdoc />
        public IObservable<IMaturedBlockDeposits[]> OnMaturedBlockDepositsPushed { get; }

        public MaturedBlockReceiver(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maturedBlockDepositStream = new ReplaySubject<IMaturedBlockDeposits[]>(1);
            this.OnMaturedBlockDepositsPushed = this.maturedBlockDepositStream.AsObservable();
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
        public void Dispose()
        {
            this.maturedBlockDepositStream?.Dispose();
        }
    }
}