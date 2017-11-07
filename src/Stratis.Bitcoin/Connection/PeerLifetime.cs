using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A class that will manage the lifetime of connected peers and be in charge of banning malicious peers.
    /// </summary>
    /// <remarks>
    /// By default peers are banned for 24 hours, this value can change using configuration (-bantime=[seconds]).
    /// </remarks>
    public interface IPeerLifetime
    {
        /// <summary>
        /// Set a peer as banned.
        /// </summary>
        /// <param name="endpoint">The endpoint to set that it was banned.</param>
        /// <param name="reason">An optional reason for the ban, the 'reason' is only use for tracing.</param>
        void BanPeer(IPEndPoint endpoint, string reason = null);

        /// <summary>
        /// Check if a peer is banned.
        /// </summary>
        /// <param name="endpoint">The endpoint to check if it was banned.</param>
        /// <returns><c>True</c>If the peer was banned.</returns>
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
        /// <param name="banUntil">The date when the ban will expire.</param>
        void BanPeer(IPEndPoint endpoint, DateTime banUntil);

        /// <summary>
        /// Get information about a banned peer.
        /// </summary>
        /// <param name="endpoint">The endpoint to query.</param>
        /// <returns>The <see cref="DateTime"/> of the end of the ban or null if the peer was not found.</returns>
        DateTime? TryGetBannedPeer(IPEndPoint endpoint);
    }

    /// <summary>
    /// A temporary memory store for banned peers.
    /// </summary>
    public class MemoryBanStore : IBanStore
    {
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
    /// A class that manages the lifetime of a peer.
    /// </summary>
    public class PeerLifetime : IPeerLifetime
    {
        /// <summary>A connection manager of peers.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>A store of banned peers.</summary>
        private readonly IBanStore banStore;

        /// <summary>Functionality of date and time.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Functionality of date and time.</summary>
        private readonly ConnectionManagerSettings connectionManagerSettings;

        public PeerLifetime(IConnectionManager connectionManager, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, NodeSettings nodeSettings)//, IBanStore banStore)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.connectionManager = connectionManager;
            this.banStore =  new MemoryBanStore();// banStore;
            this.dateTimeProvider = dateTimeProvider;
            this.connectionManagerSettings = nodeSettings.ConnectionManager;
        }

        /// <inheritdoc />
        public void BanPeer(IPEndPoint endpoint, string reason = null)
        {
            Guard.NotNull(endpoint, nameof(endpoint));

            this.logger.LogTrace("()");

            var banPeer = true;
            var peer = this.connectionManager.ConnectedNodes.FindByEndpoint(endpoint);
            if (peer != null)
            {
                ConnectionManagerBehavior peerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                if (!peerBehavior.Whitelisted)
                {
                    banPeer = false;
                    this.logger.LogDebug("Peer '{0}' banned for reason: '{1}'", endpoint, reason);
                    peer.DisconnectAsync($"The peer was banned, reason: {reason}");
                }
                else this.logger.LogTrace("Peer '{0}' whitelisted, reason: '{1}', not banned!", reason, endpoint);
            }

            if (banPeer)
            {
                this.banStore.BanPeer(endpoint, this.dateTimeProvider.GetUtcNow().AddSeconds(this.connectionManagerSettings.BanTime));
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool IsBanned(IPEndPoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));

            var peerBannedUntil = this.banStore.TryGetBannedPeer(endpoint);

            return peerBannedUntil > this.dateTimeProvider.GetUtcNow();
        }
    }
}
