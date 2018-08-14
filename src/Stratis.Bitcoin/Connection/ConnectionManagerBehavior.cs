using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public interface IConnectionManagerBehavior : INetworkPeerBehavior
    {
        ConnectionManager ConnectionManager { get; }

        bool Inbound { get; }

        bool Whitelisted { get; }

        bool OneTry { get; }
    }

    public class ConnectionManagerBehavior : NetworkPeerBehavior, IConnectionManagerBehavior
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

            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);

            this.logger.LogTrace("(-)");
        }

        private async Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(peer.State), peer.State);

            try
            {
                if (peer.State == NetworkPeerState.HandShaked)
                {
                    this.ConnectionManager.AddConnectedPeer(peer);
                    this.infoLogger.LogInformation("Peer '{0}' connected ({1}), agent '{2}', height {3}", peer.RemoteSocketEndpoint, this.Inbound ? "inbound" : "outbound", peer.PeerVersion.UserAgent, peer.PeerVersion.StartHeight);
                    await peer.SendMessageAsync(new SendHeadersPayload()).ConfigureAwait(false);
                }

                if ((peer.State == NetworkPeerState.Failed) || (peer.State == NetworkPeerState.Offline))
                {
                    this.infoLogger.LogInformation("Peer '{0}' offline, reason: '{1}'.", peer.RemoteSocketEndpoint, peer.DisconnectReason?.Reason ?? "unknown");

                    this.ConnectionManager.RemoveConnectedPeer(peer, "Peer offline");
                }
            }
            catch (OperationCanceledException)
            {
            }

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);

            this.logger.LogTrace("(-)");
        }
    }
}