using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// Contract for network peer banning provider.
    /// </summary>
    /// <remarks>
    /// Peers are banned for <see cref="ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds"/> seconds (default is 24h), this value can change using configuration (-bantime=[seconds]).
    /// </remarks>
    public interface IPeerBanning
    {
        /// <summary>
        /// Set a peer as banned.
        /// </summary>
        /// <param name="endpoint">The endpoint to set that it was banned.</param>
        /// <param name="banTimeSeconds">The time in seconds this peer should be banned.</param>
        /// <param name="reason">An optional reason for the ban, the 'reason' is only use for tracing.</param>
        void BanPeer(IPEndPoint endpoint, int banTimeSeconds, string reason = null);

        /// <summary>
        /// Check if a peer is banned.
        /// </summary>
        /// <param name="endpoint">The endpoint to check if it was banned.</param>
        /// <returns><c>true</c> if the peer was banned.</returns>
        bool IsBanned(IPEndPoint endpoint);
    }

    /// <summary>
    /// An implementation of<see cref="IPeerBanning"/>.
    /// This will manage banning of peers and checking for banned peers.
    /// </summary>
    public class PeerBanning : IPeerBanning
    {
        /// <summary>A connection manager of peers.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Functionality of date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Functionality of PeerAddressManager.</summary> 
        private readonly IPeerAddressManager peerAddressManager;

        public PeerBanning(IConnectionManager connectionManager, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IPeerAddressManager peerAddressManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.connectionManager = connectionManager;
            this.dateTimeProvider = dateTimeProvider;
            this.peerAddressManager = peerAddressManager;
        }

        /// <inheritdoc />
        public void BanPeer(IPEndPoint endpoint, int banTimeSeconds, string reason = null)
        {
            Guard.NotNull(endpoint, nameof(endpoint));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(endpoint), endpoint, nameof(reason), reason);

            reason = reason ?? "unknown";

            bool banPeer = true;
            INetworkPeer peer = this.connectionManager.ConnectedPeers.FindByEndpoint(endpoint);
            if (peer != null)
            {
                ConnectionManagerBehavior peerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                if (!peerBehavior.Whitelisted)
                {
                    peer.Disconnect($"The peer was banned, reason: {reason}");
                }
                else
                {
                    banPeer = false;
                    this.logger.LogTrace("Peer '{0}' is whitelisted, for reason '{1}' it was not banned!", endpoint, reason);
                }
            }

            if (!banPeer) return;

            PeerAddress peerAddress = this.peerAddressManager.FindPeer(endpoint);

            if (peerAddress == null) return;

            peerAddress.BanDate = this.dateTimeProvider.GetUtcNow();
            peerAddress.BanUntil = this.dateTimeProvider.GetUtcNow().AddSeconds(banTimeSeconds);

            this.logger.LogDebug("Peer '{0}' banned for reason '{1}'.", endpoint, reason);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool IsBanned(IPEndPoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));

            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            PeerAddress peerAddress = this.peerAddressManager.FindPeer(endpoint);

            if (peerAddress == null)
                return false;

            return peerAddress.BanUntil > this.dateTimeProvider.GetUtcNow();
        }
    }
}
