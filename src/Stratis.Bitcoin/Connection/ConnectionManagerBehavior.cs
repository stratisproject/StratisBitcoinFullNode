using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public class ConnectionManagerBehavior : NodeBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Instance logger that we use for logging of INFO level messages that are visible on the console.
        /// <para>Unlike <see cref="logger"/>, this one is created without prefix for the nicer console output.</para>
        /// </summary>
        private readonly ILogger infoLogger;

        public ConnectionManager ConnectionManager { get; private set; }
        public bool Inbound { get; private set; }
        public bool Whitelisted { get; internal set; }
        public bool OneTry { get; internal set; }

        private ChainHeadersBehavior chainHeadersBehavior;

        public ConnectionManagerBehavior(bool inbound, IConnectionManager connectionManager, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.infoLogger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;

            this.Inbound = inbound;
            this.ConnectionManager = connectionManager as ConnectionManager;
        }

        public override object Clone()
        {
            return new ConnectionManagerBehavior(this.Inbound, this.ConnectionManager, this.loggerFactory)
            {
                OneTry = this.OneTry,
                Whitelisted = this.Whitelisted,
            };
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
            this.chainHeadersBehavior = this.AttachedNode.Behaviors.Find<ChainHeadersBehavior>();

            this.logger.LogTrace("(-)");
        }

        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (this.chainHeadersBehavior.InvalidHeaderReceived && !this.Whitelisted)
            {
                node.DisconnectAsync("Invalid block received");
            }

            this.logger.LogTrace("(-)");
        }

        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(node), node.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(node.State), node.State);

            if (node.State == NodeState.HandShaked)
            {
                this.ConnectionManager.AddConnectedNode(node);
                this.infoLogger.LogInformation("Node {0} connected ({1}), agent {2}, height {3}", node.RemoteSocketEndpoint, this.Inbound ? "inbound" : "outbound", node.PeerVersion.UserAgent, node.PeerVersion.StartHeight);
                node.SendMessageAsync(new SendHeadersPayload());
            }

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Offline))
            {
                this.infoLogger.LogInformation("Node {0} offline.", node.RemoteSocketEndpoint);

                NodeDisconnectReason reason = node.DisconnectReason;
                if ((reason != null) && !string.IsNullOrEmpty(reason.Reason))
                    this.infoLogger.LogInformation("Reason: {0}", reason.Reason);

                this.ConnectionManager.RemoveConnectedNode(node);
            }

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;

            this.logger.LogTrace("(-)");
        }
    }
}