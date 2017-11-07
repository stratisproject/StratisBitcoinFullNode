using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// A behaviour that will manage the lifetime of peers
    /// </summary>
    public class PeerLifetimeBehavior : NodeBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Handle the lifetime of a peer.</summary>
        private readonly IPeerLifetime peerLifetime;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ConnectionManager ConnectionManager { get; private set; }

        private ChainHeadersBehavior chainHeadersBehavior;

        private ConnectionManagerBehavior connectionManagerBehavior;

        public PeerLifetimeBehavior(IConnectionManager connectionManager, ILoggerFactory loggerFactory, IPeerLifetime peerLifetime)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.peerLifetime = peerLifetime;
            this.ConnectionManager = connectionManager as ConnectionManager;
        }

        public override object Clone()
        {
            return new PeerLifetimeBehavior(this.ConnectionManager, this.loggerFactory, this.peerLifetime);
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            var node = this.AttachedNode;
            if (node.State == NodeState.Connected)
            {
                if (this.peerLifetime.IsBanned(node.RemoteSocketEndpoint))
                {
                    this.logger.LogDebug("Node '{0}' was previously banned", node.RemoteSocketEndpoint);
                    node.DisconnectAsync("A banned node tried to connect");
                    return;
                }
            }

            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
            this.chainHeadersBehavior = this.AttachedNode.Behaviors.Find<ChainHeadersBehavior>();
            this.connectionManagerBehavior = this.AttachedNode.Behaviors.Find<ConnectionManagerBehavior>();

            this.logger.LogTrace("(-)");
        }

        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (this.chainHeadersBehavior.InvalidHeaderReceived && !this.connectionManagerBehavior.Whitelisted)
            {
                this.peerLifetime.BanPeer(node.RemoteSocketEndpoint);
                node.DisconnectAsync("Invalid block received");
            }

            this.logger.LogTrace("(-)");
        }

        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(node), node.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(node.State), node.State);

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;

            this.logger.LogTrace("(-)");
        }
    }
}