using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
    /// <remarks>
    /// Each component that creates instances of <see cref="NetworkPeer"/> should be responsible for disposing it.
    /// <para>
    /// Implementing this functionality in such components will lead to having similar code in these components.
    /// Instead, this class could be used in order to provide such functionality.
    /// This means that the responsibility for destroying the peer can delegated to this class, which simplifies the
    /// code of the owning component.
    /// </para>
    /// <para>
    /// When a new peer is created (and the <see cref="OnPeerDisconnectedHandler"/> callback is used as an <see cref="NetworkPeer.onDisconnected"/> in the constructor)
    /// by a component that utilizes this class, <see cref="AddPeer"/> should be used to inform  this class about it. Once the peer is added, the owning component no
    /// longer needs to care about this peer's disposal.
    /// When a peer disconnects, this class will invoke peer's disposal in a separated task.
    /// Also when <see cref="Dispose"/> is called, all connected peers added to this component will be disposed.
    /// </para>
    /// </remarks>
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

        /// <summary>Gets the connected inbound peers count.</summary>
        public int ConnectedInboundPeersCount
        {
            get { return this.connectedPeers.Count(p => p.Value.Inbound); }
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
            this.onPeerDisposed?.Invoke(peer);

            peer.Dispose();

            this.connectedPeers.TryRemove(peer.Connection.Id, out INetworkPeer unused);

            return Task.CompletedTask;
        }

        /// <summary>Handles peer's disconnection.</summary>
        /// <param name="peer">Peer which disposal should be safely handled.</param>
        public void OnPeerDisconnectedHandler(INetworkPeer peer)
        {
            this.peersToDispose.Enqueue(peer);
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
            this.peersToDispose.Dispose();

            foreach (INetworkPeer peer in this.connectedPeers.Values)
            {
                peer.Disconnect("Node shutdown");

                this.logger.LogTrace("Disposing and waiting for connection ID {0}.", peer.Connection.Id);

                peer.Dispose();
            }

            this.connectedPeers.Clear();
        }
    }
}
