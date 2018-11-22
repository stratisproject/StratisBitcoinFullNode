using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This component is responsible for streaming a change in the federated leader that
    /// other components can subscribe to.  Like <see cref="ISignedMultisigTransactionBroadcaster"/>.
    /// </summary>
    public interface ILeaderReceiver : IDisposable
    {
        /// <summary>
        /// Notifies subscribed and future observers about the arrival of a change in federatated leader.
        /// </summary>
        /// <param name="leaderProvider">
        /// Provides the current federated leader.
        /// </param>
        void ReceiveLeader(ILeaderProvider leaderProvider);

        /// <summary>
        /// Streams a change in federated leader.
        /// </summary>
        IObservable<ILeaderProvider> LeaderProvidersStream { get; }
    }
}
