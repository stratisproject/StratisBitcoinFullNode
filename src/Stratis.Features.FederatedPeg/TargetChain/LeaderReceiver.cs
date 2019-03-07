using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// This component is responsible for streaming a change in the federated leader that
    /// other components can subscribe to.  Like <see cref="ISignedMultisigTransactionBroadcaster"/>.
    /// </summary>
    public interface ILeaderReceiver : IDisposable
    {
        /// <summary>
        /// Notifies subscribed and future observers about the arrival of a change in federated leader.
        /// </summary>
        /// <param name="leaderProvider">
        /// Provides the current federated leader.
        /// </param>
        void PushLeader(ILeaderProvider leaderProvider);

        /// <summary>
        /// Streams a change in federated leader.
        /// </summary>
        IObservable<ILeaderProvider> LeaderProvidersStream { get; }
    }

    /// <inheritdoc />
    public class LeaderReceiver : ILeaderReceiver
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
        public void PushLeader(ILeaderProvider leaderProvider)
        {
            this.logger.LogInformation("Received federated leader: {0}.", leaderProvider.CurrentLeaderKey);
            this.leaderProvidersStream.OnNext(leaderProvider);
        }

        public void Dispose()
        {
            this.leaderProvidersStream?.Dispose();
        }
    }
}
