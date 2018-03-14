using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
    public class NetworkPeerDisposer : IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Callback that is called before the peer is disposed.</summary>
        private readonly Action<INetworkPeer> onPeerDisposed;

        /// <summary>Queue of disconnected peers to be disposed.</summary>
        private readonly AsyncQueue<INetworkPeer> peersToDispose;

        /// <summary>Mapping of connected peers by their connection ID.</summary>
        private readonly ConcurrentDictionary<int, INetworkPeer> connectedPeers;

        /// <summary>Gets the connected peers count.</summary>
        public int ConnectedPeersCount
        {
            get { return this.connectedPeers.Count; }
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkPeerDisposer" /> class.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="onPeerDisposed">Callback that is called before the peer is disposed.</param>
        public NetworkPeerDisposer(ILoggerFactory loggerFactory, Action<INetworkPeer> onPeerDisposed = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.onPeerDisposed = onPeerDisposed; 
            this.connectedPeers = new ConcurrentDictionary<int, INetworkPeer>();

            this.peersToDispose = new AsyncQueue<INetworkPeer>(this.OnEnqueueAsync);
        }

        /// <summary>
        /// Callback that is invoked whenever a new peer is added to the <see cref="peersToDispose" />.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private Task OnEnqueueAsync(INetworkPeer peer, CancellationToken cancellationToken)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peer), peer.RemoteSocketAddress);

            this.onPeerDisposed?.Invoke(peer);
            
            peer.Dispose();

            this.connectedPeers.TryRemove(peer.Connection.Id, out INetworkPeer unused);

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        /// <summary>Handles peer's disconnection.</summary>
        /// <param name="peer">Peer which disposal should be safely handled.</param>
        public void OnPeerDisconnectedHandler(INetworkPeer peer)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peer), peer.RemoteSocketAddress);

            this.peersToDispose.Enqueue(peer);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds the peer to the collection of connected peers.
        /// </summary>
        /// <param name="peer">The peer to add.</param>
        public void AddPeer(INetworkPeer peer)
        {
            this.connectedPeers.TryAdd(peer.Connection.Id, peer);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.peersToDispose.Dispose();

            foreach (INetworkPeer peer in this.connectedPeers.Values)
            {
                peer.Disconnect("Node shutdown");

                this.logger.LogTrace("Disposing and waiting for connection ID {0}.", peer.Connection.Id);

                peer.Dispose();
            }

            this.connectedPeers.Clear();

            this.logger.LogTrace("(-)");
        }
    }
}
