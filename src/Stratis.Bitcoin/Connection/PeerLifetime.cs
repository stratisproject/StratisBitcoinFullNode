using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A class that will manage the lifetime of connected peers and be in charge of banning peers that are malicious.
    /// </summary>
    public interface IPeerLifetime
    {
        void BanPeer(IPEndPoint endPoint, string reason = null);

        bool IsBanned(IPEndPoint endPoint);
    }

    /// <summary>
    /// A class that will manage the lifetime of connected peers and be in charge of banning peers that are malicious.
    /// </summary>
    public interface IBanStore
    {
        void BanPeer(IPEndPoint endPoint, DateTime banUntil);

        DateTime? TryGetBanedPeer(IPEndPoint endPoint);
    }

    /// <summary>
    /// A temporary memory store for banned peers.
    /// </summary>
    public class MemoryBanStore : IBanStore
    {
        private readonly ConcurrentDictionary<IPEndPoint, DateTime> banned = new ConcurrentDictionary<IPEndPoint, DateTime>();

        public void BanPeer(IPEndPoint endPoint, DateTime banUntil)
        {
            this.banned.AddOrUpdate(endPoint, banUntil, (point, time) => banUntil);
        }

        public DateTime? TryGetBanedPeer(IPEndPoint endPoint)
        {
            if (this.banned.TryGetValue(endPoint, out DateTime banUntil))
                return banUntil;

            return null;
        }
    }

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

        public PeerLifetime(IConnectionManager connectionManager, ILoggerFactory loggerFactory, IBanStore banStore, IDateTimeProvider dateTimeProvider)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.connectionManager = connectionManager;
            this.banStore = banStore;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc />
        public void BanPeer(IPEndPoint endPoint, string reason = null)
        {
            Guard.NotNull(endPoint, nameof(endPoint));

            this.logger.LogTrace("()");

            this.banStore.BanPeer(endPoint, this.dateTimeProvider.GetUtcNow().AddDays(7));

            var peer = this.connectionManager.ConnectedNodes.FindByEndpoint(endPoint);
            if (peer != null)
            {
                ConnectionManagerBehavior peerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                if (!peerBehavior.Whitelisted)
                {
                    this.logger.LogDebug("Peer '{0}' banned for reason: '{1}'", endPoint, reason);
                    peer.DisconnectAsync($"The peer was banned, reason: {reason}");
                }
                else this.logger.LogTrace("Peer '{0}' whitelisted, reason: '{1}', not banned!", reason, endPoint);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool IsBanned(IPEndPoint endPoint)
        {
            Guard.NotNull(endPoint, nameof(endPoint));

            var peerBanedUntil = this.banStore.TryGetBanedPeer(endPoint);
            if (peerBanedUntil == null)
                return false;

            return peerBanedUntil > this.dateTimeProvider.GetUtcNow();
        }
    }
}
