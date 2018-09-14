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

        private readonly IConnectionManager connectionManager;

        public bool Whitelisted { get; internal set; }

        public bool OneTry { get; internal set; }

        public ConnectionManagerBehavior(IConnectionManager connectionManager, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.infoLogger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;

            this.connectionManager = connectionManager;
        }

        public override object Clone()
        {
            return new ConnectionManagerBehavior(this.connectionManager, this.loggerFactory)
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
                    this.connectionManager.AddConnectedPeer(peer);
                    this.infoLogger.LogInformation("Peer '{0}' connected ({1}), agent '{2}', height {3}", peer.RemoteSocketEndpoint, peer.Inbound ? "inbound" : "outbound", peer.PeerVersion.UserAgent, peer.PeerVersion.StartHeight);
                    await peer.SendMessageAsync(new SendHeadersPayload()).ConfigureAwait(false);
                }

                if ((peer.State == NetworkPeerState.Failed) || (peer.State == NetworkPeerState.Offline))
                {
                    this.infoLogger.LogInformation("Peer '{0}' offline, reason: '{1}'.", peer.RemoteSocketEndpoint, peer.DisconnectReason?.Reason ?? "unknown");

                    this.connectionManager.RemoveConnectedPeer(peer, "Peer offline");
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
            this.connectionManager.PeerDisconnected(this.AttachedPeer.Connection.Id);

            this.logger.LogTrace("(-)");
        }
    }
}