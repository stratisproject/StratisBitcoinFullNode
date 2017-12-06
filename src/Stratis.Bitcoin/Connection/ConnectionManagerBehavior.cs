using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public class ConnectionManagerBehavior : NetworkPeerBehavior
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

            this.AttachedPeer.StateChanged += this.AttachedNode_StateChanged;
            this.chainHeadersBehavior = this.AttachedPeer.Behaviors.Find<ChainHeadersBehavior>();

            this.logger.LogTrace("(-)");
        }

        private void AttachedNode_StateChanged(NetworkPeer node, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(node), node.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(node.State), node.State);

            if (node.State == NetworkPeerState.HandShaked)
            {
                this.ConnectionManager.AddConnectedNode(node);
                this.infoLogger.LogInformation("Node '{0}' connected ({1}), agent '{2}', height {3}", node.RemoteSocketEndpoint, this.Inbound ? "inbound" : "outbound", node.PeerVersion.UserAgent, node.PeerVersion.StartHeight);
                Task unused = node.SendMessageAsync(new SendHeadersPayload());
            }

            if ((node.State == NetworkPeerState.Failed) || (node.State == NetworkPeerState.Offline))
            {
                this.infoLogger.LogInformation("Node '{0}' offline, reason: '{1}'.", node.RemoteSocketEndpoint, node.DisconnectReason?.Reason ?? "unknown");
                this.ConnectionManager.RemoveConnectedNode(node);
            }

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.StateChanged -= this.AttachedNode_StateChanged;

            this.logger.LogTrace("(-)");
        }
    }
}
