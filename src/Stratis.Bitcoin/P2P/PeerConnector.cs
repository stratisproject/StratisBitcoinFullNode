using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Contract for <see cref="PeerConnector"/>.</summary>
    public interface IPeerConnector : IDisposable
    {
        /// <summary>The collection of peers the connector is currently connected to.</summary>
        NetworkPeerCollection ConnectedPeers { get; }

        /// <summary>
        /// Selects a peer from the peer selector.
        /// <para>
        /// Each implementation of <see cref="PeerConnector"/> will have its own implementation
        /// of this method.
        /// </para>
        /// <para>
        /// Refer to <see cref="IPeerSelector.SelectPeer()"/> for more details.
        /// </para>
        /// </summary>
        PeerAddress FindPeerToConnectTo();

        /// <summary>Peer connector initialization as called by the <see cref="ConnectionManager"/>.</summary>
        void Initialize(IConnectionManager connectionManager);

        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        int MaximumNodeConnections { get; set; }

        /// <summary>Specification of requirements the <see cref="PeerConnector"/> has when connecting to other peers.</summary>
        NetworkPeerRequirement Requirements { get; }

        /// <summary>
        /// Adds a peer to the <see cref="ConnectedPeers"/>.
        /// <para>
        /// This will only happen if the peer successfully handshaked with another.
        /// </para>
        /// </summary>
        void AddPeer(NetworkPeer peer);

        /// <summary>
        /// Removes a given peer from the <see cref="ConnectedPeers"/>.
        /// <para>
        /// This will happen if the peer state changed to "disconnecting", "failed" or "offline".
        /// </para>
        /// </summary>
        void RemovePeer(NetworkPeer peer);

        /// <summary>
        /// Starts an asynchronous loop that connects to peers in one second intervals.
        /// <para>
        /// If the maximum amount of connections has been reached (<see cref="MaximumNodeConnections"/>), the action gets skipped.
        /// </para>
        /// </summary>
        void StartConnectAsync();
    }

    /// <summary>
    /// Connects to peers asynchronously.
    /// </summary>
    public abstract class PeerConnector : IPeerConnector
    {
        /// <summary>The async loop we need to wait upon before we can dispose of this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Collection of connected peers that is managed by the <see cref="ConnectionManager"/>.
        /// </summary>
        private IReadOnlyNetworkPeerCollection connectedPeers;

        /// <inheritdoc/>
        public NetworkPeerCollection ConnectedPeers { get; private set; }

        /// <summary>The parameters cloned from the connection manager.</summary>
        public NetworkPeerConnectionParameters CurrentParameters { get; private set; }

        /// <summary>Logger factory to create loggers.</summary>
        private ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <inheritdoc/>
        public int MaximumNodeConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        protected INodeLifetime nodeLifetime;

        /// <summary>User defined node settings.</summary>
        public NodeSettings NodeSettings;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        protected IPeerAddressManager peerAddressManager;

        /// <summary>Factory for creating P2P network peers.</summary>
        private INetworkPeerFactory networkPeerFactory;

        /// <inheritdoc/>
        public NetworkPeerRequirement Requirements { get; internal set; }

        /// <summary>Parameterless constructor for dependency injection.</summary>
        protected PeerConnector(
            IAsyncLoopFactory asyncLoopFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.networkPeerFactory = networkPeerFactory;
            this.nodeLifetime = nodeLifetime;
            this.NodeSettings = nodeSettings;
            this.peerAddressManager = peerAddressManager;
        }

        /// <inheritdoc/>
        public void Initialize(IConnectionManager connectionManager)
        {
            this.connectedPeers = connectionManager.ConnectedNodes;

            this.CurrentParameters = connectionManager.Parameters.Clone();
            this.CurrentParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, connectionManager, this.loggerFactory));
            this.CurrentParameters.TemplateBehaviors.Add(new PeerConnectorBehaviour(this));

            OnInitialize();
        }

        /// <inheritdoc/>
        public void AddPeer(NetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            this.ConnectedPeers.Add(peer);
        }

        /// <summary>Determines whether or not a connector can be started.</summary>
        public abstract bool CanStartConnect { get; }

        /// <summary>Specific peer connector initialization for each concrete implementation of this class.</summary>
        public abstract void OnInitialize();

        /// <summary>Start up logic specific to each concrete implementation of this class.</summary>
        public abstract void OnStartConnect();

        /// <inheritdoc/>
        public void RemovePeer(NetworkPeer peer)
        {
            this.ConnectedPeers.Remove(peer);
        }

        /// <summary>
        /// <c>true</c> if the peer is already connected.
        /// </summary>
        /// <param name="ipEndpoint">The endpoint to check.</param>
        internal bool IsPeerConnected(IPEndPoint ipEndpoint)
        {
            return this.connectedPeers.FindByEndpoint(ipEndpoint) != null;
        }

        /// <inheritdoc/>
        public void StartConnectAsync()
        {
            if (!this.CanStartConnect)
                return;

            this.OnStartConnect();

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                await this.ConnectAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second);
        }

        /// <summary>Attempts to connect to a random peer.</summary>
        private async Task ConnectAsync()
        {
            if (!this.peerAddressManager.Peers.Any())
                return;

            if (this.ConnectedPeers.Count >= this.MaximumNodeConnections)
                return;

            NetworkPeer peer = null;

            try
            {
                PeerAddress peerAddress = this.FindPeerToConnectTo();
                if (peerAddress == null)
                {
                    Task.Delay(TimeSpans.TenSeconds.Milliseconds).Wait(this.nodeLifetime.ApplicationStopping);
                    return;
                }

                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    timeoutTokenSource.CancelAfter(5000);

                    this.peerAddressManager.PeerAttempted(peerAddress.NetworkAddress.Endpoint, this.dateTimeProvider.GetUtcNow());

                    var clonedConnectParamaters = this.CurrentParameters.Clone();
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    peer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(this.network, peerAddress.NetworkAddress, clonedConnectParamaters).ConfigureAwait(false);
                    await peer.VersionHandshakeAsync(this.Requirements, timeoutTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                peer?.DisconnectWithException("Error while connecting", exception);
            }
        }

        /// <summary>Disconnects all the peers in <see cref="ConnectedPeers"/>.</summary>
        private void Disconnect()
        {
            this.ConnectedPeers.DisconnectAll("Node shutdown.");
        }

        /// <inheritdoc/>
        public abstract PeerAddress FindPeerToConnectTo();

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.Disconnect();
        }
    }
}