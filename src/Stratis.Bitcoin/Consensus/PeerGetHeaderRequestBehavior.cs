using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Bans and disconnects peers that sends a <see cref="GetHeadersPayload"/> request, more than <see cref="GetHeaderRequestCountThreshold"/> times,
    /// within a certain time frame.
    /// </summary>
    /// <remarks>
    /// TODO:
    /// This behavior is a tempory work around to peers that spam the node with too many <see cref="GetHeadersPayload"/> requests.
    /// It will be changed in the future once a more in-depth and thorough implementation has been agreed upon.
    /// </remarks>
    public sealed class RateLimitingBehavior : NetworkPeerBehavior
    {
        /// <summary>How long the offending peer will be banned for.</summary>
        private const int BanDurationSeconds = 3600;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The last header that was requested.</summary>
        private uint256 getHeaderLastRequestHash;

        /// <summary>The last time the header was requested.</summary>
        private DateTimeOffset? getHeaderLastRequestedTimestamp;

        /// <summary>The amount of times the same request has been made.</summary>
        private int getHeaderRequestCount;

        /// <summary>The threshold after which the node will be banned and disconnected.</summary>
        private const int GetHeaderRequestCountThreshold = 10;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Contract for peer banning behavior.</summary>
        private readonly IPeerBanning peerBanning;

        public RateLimitingBehavior(
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IPeerBanning peerBanning)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerBanning = peerBanning;
        }

        public override object Clone()
        {
            return new RateLimitingBehavior(this.dateTimeProvider, this.loggerFactory, this.peerBanning);
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceived, true);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceived);
        }

        /// <summary>
        /// Processes an incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        private Task OnMessageReceived(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetHeadersPayload getHeaders:
                    this.HandleGetHeaders(getHeaders);
                    break;

                default:
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines whether or not a peer asked for the same set of headers within 60 seconds.
        /// <para>
        /// If the same set of headers was requested more than <see cref="GetHeaderRequestCountThreshold"/>, it will be banned
        /// and disconnected.
        /// </para>
        /// </summary>
        private void HandleGetHeaders(GetHeadersPayload getHeaders)
        {
            var blockLocatorHash = getHeaders.BlockLocator.Blocks.FirstOrDefault();
            if (blockLocatorHash == null)
            {
                this.logger.LogTrace("(-)[EMPTY_BLOCKLOCATOR]");
                return;
            }

            // Is the last requested hash the same as this request.
            if (this.getHeaderLastRequestHash == blockLocatorHash)
            {
                this.logger.LogDebug($"{this.AttachedPeer.PeerEndPoint} block locator matches previous, count {this.getHeaderRequestCount}");

                // Was this hash requested less than 60 seconds ago.
                if (this.getHeaderLastRequestedTimestamp > this.dateTimeProvider.GetUtcNow().AddSeconds(-60))
                {
                    this.getHeaderRequestCount++;
                }
                else
                {
                    this.getHeaderLastRequestedTimestamp = this.dateTimeProvider.GetUtcNow();
                    this.getHeaderRequestCount = 0;

                    this.logger.LogTrace("(-)[LAST_REQUESTED_WINDOW_ELAPSED]");
                    return;
                }

                // If the same header was requested more than 3 times in the last 60 seconds,
                // ban and disconnect the peer for 1 hour.
                if (this.getHeaderRequestCount >= GetHeaderRequestCountThreshold)
                {
                    this.peerBanning.BanAndDisconnectPeer(this.AttachedPeer.PeerEndPoint, BanDurationSeconds, $"Banned via rate limiting for {BanDurationSeconds} seconds.");
                    this.logger.LogDebug("{0} banned via {1} for {2} seconds.", this.AttachedPeer.PeerEndPoint, this.GetType().Name, BanDurationSeconds);
                }
            }
            else
            {
                this.getHeaderLastRequestHash = blockLocatorHash;
                this.getHeaderRequestCount = 0;
            }
        }
    }
}
