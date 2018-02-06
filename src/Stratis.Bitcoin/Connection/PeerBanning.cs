using System;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
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
    /// A class that will manage the lifetime of connected peers and be in charge of banning peers that are malicious.
    /// </summary>
    public interface IBanStore
    {
        /// <summary>
        /// Set a peer endpoint to a banned state.
        /// </summary>
        /// <param name="endpoint">The endpoint to set that it was banned.</param>
        /// <param name="banUntil">The UTC date of when the ban will expire.</param>
        void BanPeer(IPEndPoint endpoint, DateTime banUntil);

        /// <summary>
        /// Get information about a banned peer.
        /// </summary>
        /// <param name="endpoint">The endpoint to query.</param>
        /// <returns>The expiration time of the peer's ban, or <c>null</c> if the peer was not found.</returns>
        DateTime? TryGetBannedPeer(IPEndPoint endpoint);
    }

    /// <summary>
    /// A memory store for banned peers.
    /// </summary>
    public class MemoryBanStore : IBanStore
    {
        /// <summary>A collection that will store banned peers.</summary>
        private readonly ConcurrentDictionary<IPAddress, DateTime> banned = new ConcurrentDictionary<IPAddress, DateTime>();

        /// <inheritdoc />
        public void BanPeer(IPEndPoint endpoint, DateTime banUntil)
        {
            this.banned.AddOrUpdate(endpoint.Address, banUntil, (point, time) => banUntil);
        }

        /// <inheritdoc />
        public DateTime? TryGetBannedPeer(IPEndPoint endpoint)
        {
            this.banned.TryGetValue(endpoint.Address, out DateTime banUntil);

            return banUntil;
        }
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

        /// <summary>A store of banned peers.</summary>
        private readonly IBanStore banStore;

        /// <summary>Functionality of date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public PeerBanning(IConnectionManager connectionManager, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, NodeSettings nodeSettings)//, IBanStore banStore)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.connectionManager = connectionManager;
            this.dateTimeProvider = dateTimeProvider;

            // TODO: MemoryBanStore should be replaced with the address manager store
            this.banStore = new MemoryBanStore();
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

            if (banPeer)
            {
                this.logger.LogDebug("Peer '{0}' banned for reason '{1}'.", endpoint, reason);
                this.banStore.BanPeer(endpoint, this.dateTimeProvider.GetUtcNow().AddSeconds(banTimeSeconds));
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool IsBanned(IPEndPoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));

            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            DateTime? peerBannedUntil = this.banStore.TryGetBannedPeer(endpoint);

            return peerBannedUntil > this.dateTimeProvider.GetUtcNow();
        }
    }
}
