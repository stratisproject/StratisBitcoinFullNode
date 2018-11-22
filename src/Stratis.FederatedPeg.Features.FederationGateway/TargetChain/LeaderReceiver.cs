using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class LeaderReceiver : ILeaderReceiver, IDisposable
    {
        private readonly ReplaySubject<ILeaderProvider> leaderProvidersStream;

        private readonly ILogger logger;

        public LeaderReceiver(ILoggerFactory loggerFactory)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.leaderProvidersStream = new ReplaySubject<ILeaderProvider>(1);
            this.LeaderProvidersStream = this.leaderProvidersStream.AsObservable();
        }

        /// <inheritdoc />
        public IObservable<ILeaderProvider> LeaderProvidersStream { get; }

        /// <inheritdoc />
        public void ReceiveLeader(ILeaderProvider leaderProvider)
        {
            this.logger.LogDebug("Received federated leader: {0}", leaderProvider.CurrentLeader);
            this.leaderProvidersStream.OnNext(leaderProvider);
        }

        public void Dispose()
        {
            this.leaderProvidersStream?.Dispose();
        }
    }
}
