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
    public sealed class RateLimitingBehavior : NetworkPeerBehavior
    {
        private const int BanDurationSeconds = 3600;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Contract for peer banning behavior.</summary>
        private readonly IPeerBanning peerBanning;

        private int getHeaderRequestCount;
        private const int GetHeaderRequestCountThreshold = 3;
        private uint256 getHeaderLastRequestHash;
        private DateTimeOffset? getHeaderLastRequestedTimestamp;

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
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        private Task OnMessageReceived(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetHeadersPayload getHeaders:
                    this.AssessPeerGetHeaderFrequency(getHeaders);
                    break;

                default:
                    break;
            }

            return Task.CompletedTask;
        }

        private void AssessPeerGetHeaderFrequency(GetHeadersPayload getHeaders)
        {
            // Is the last requested hash the same as this request.
            if (this.getHeaderLastRequestHash == getHeaders.BlockLocator.Blocks.Last())
            {
                // Was this hash requested less than 60 seconds ago.
                if (this.getHeaderLastRequestedTimestamp > this.dateTimeProvider.GetUtcNow().AddSeconds(-60))
                {
                    this.getHeaderRequestCount++;
                }
                else
                {
                    this.getHeaderLastRequestedTimestamp = this.dateTimeProvider.GetUtcNow();
                    this.getHeaderRequestCount = 0;
                    return;
                }

                // If the same header was requested more than 3 times in the last 60 seconds,
                // ban and disconnect the peer for 1 hour.
                if (this.getHeaderRequestCount > GetHeaderRequestCountThreshold)
                {
                    this.peerBanning.BanAndDisconnectPeer(this.AttachedPeer.PeerEndPoint, BanDurationSeconds, $"{this.AttachedPeer.PeerEndPoint} banned via {this.GetType().Name} for {BanDurationSeconds} seconds.");
                    this.logger.LogInformation($"{this.AttachedPeer.PeerEndPoint} was banned via {this.GetType().Name} for {BanDurationSeconds} seconds.");
                }
            }
            else
            {
                this.getHeaderLastRequestHash = getHeaders.BlockLocator.Blocks.Last();
                this.getHeaderRequestCount = 0;
            }
        }
    }
}
