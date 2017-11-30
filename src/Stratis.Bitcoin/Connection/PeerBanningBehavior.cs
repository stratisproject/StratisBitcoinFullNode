using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A behaviour that will manage the lifetime of peers.
    /// </summary>
    public class PeerBanningBehavior : NetworkPeerBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Handle the lifetime of a peer.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>The node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Instance of the <see cref="ChainHeadersBehavior"/> that belongs to the same peer as this behaviour.</summary>
        private ChainHeadersBehavior chainHeadersBehavior;

        /// <summary>Instance of the <see cref="ConnectionManagerBehavior"/> that belongs to the same peer as this behaviour.</summary>
        private ConnectionManagerBehavior connectionManagerBehavior;

        public PeerBanningBehavior(ILoggerFactory loggerFactory, IPeerBanning peerBanning, NodeSettings nodeSettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.peerBanning = peerBanning;
            this.nodeSettings = nodeSettings;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new PeerBanningBehavior(this.loggerFactory, this.peerBanning, this.nodeSettings);
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            var node = this.AttachedPeer;
            if (node.State == NetworkPeerState.Connected)
            {
                if (this.peerBanning.IsBanned(node.RemoteSocketEndpoint))
                {
                    this.logger.LogDebug("Node '{0}' was previously banned.", node.RemoteSocketEndpoint);
                    node.DisconnectAsync("A banned node tried to connect.");
                    return;
                }
            }

            this.AttachedPeer.MessageReceived += this.AttachedNode_MessageReceived;
            this.chainHeadersBehavior = this.AttachedPeer.Behaviors.Find<ChainHeadersBehavior>();
            this.connectionManagerBehavior = this.AttachedPeer.Behaviors.Find<ConnectionManagerBehavior>();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Receive message payloads from the peer.
        /// </summary>
        /// <param name="node">The peers that is sending the message.</param>
        /// <param name="message">The message payload.</param>
        private void AttachedNode_MessageReceived(NetworkPeer node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (this.chainHeadersBehavior.InvalidHeaderReceived && !this.connectionManagerBehavior.Whitelisted)
            {
                this.peerBanning.BanPeer(node.RemoteSocketEndpoint, this.nodeSettings.ConnectionManager.BanTimeSeconds);
                this.logger.LogTrace("Invalid block received from peer '{0}'.", node.RemoteSocketEndpoint);
                node.DisconnectAsync("Invalid block received.");
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived -= this.AttachedNode_MessageReceived;

            this.logger.LogTrace("(-)");
        }
    }
}
