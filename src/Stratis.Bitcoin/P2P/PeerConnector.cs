using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Contract for <see cref="PeerConnector"/>.</summary>
    public interface IPeerConnector : IDisposable
    {
        /// <summary>The collection of peers the connector is currently connected to.</summary>
        NetworkPeerCollection ConnectorPeers { get; }

        /// <summary>Peer connector initialization as called by the <see cref="ConnectionManager"/>.</summary>
        void Initialize(IConnectionManager connectionManager);

        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        int MaxOutboundConnections { get; set; }

        /// <summary>Specification of requirements the <see cref="PeerConnector"/> has when connecting to other peers.</summary>
        NetworkPeerRequirement Requirements { get; }

        /// <summary>
        /// Starts an asynchronous loop that connects to peers in one second intervals.
        /// <para>
        /// If the maximum amount of connections has been reached (<see cref="MaxOutboundConnections"/>), the action gets skipped.
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
        protected IConnectionManager connectionManager;

        /// <inheritdoc/>
        public NetworkPeerCollection ConnectorPeers { get; private set; }

        /// <summary>The parameters cloned from the connection manager.</summary>
        public NetworkPeerConnectionParameters CurrentParameters { get; private set; }

        /// <summary>Logger factory to create loggers.</summary>
        private ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <inheritdoc/>
        public int MaxOutboundConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        protected INodeLifetime nodeLifetime;

        /// <summary>User defined connection settings.</summary>
        public ConnectionManagerSettings ConnectionSettings;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        protected IPeerAddressManager peerAddressManager;

        /// <summary>Tracker for endpoints known to be self.</summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        /// <summary>Factory for creating P2P network peers.</summary>
        private INetworkPeerFactory networkPeerFactory;

        /// <inheritdoc/>
        public NetworkPeerRequirement Requirements { get; internal set; }

        /// <summary>Default time interval between making a connection attempt.</summary>
        private readonly TimeSpan connectionInterval;

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        /// <summary>Constructor for dependency injection.</summary>
        protected PeerConnector(
            IAsyncLoopFactory asyncLoopFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            ISelfEndpointTracker selfEndpointTracker)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectorPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.networkPeerFactory = networkPeerFactory;
            this.nodeLifetime = nodeLifetime;
            this.ConnectionSettings = connectionSettings;
            this.peerAddressManager = peerAddressManager;
            this.networkPeerDisposer = new NetworkPeerDisposer(this.loggerFactory, this.OnPeerDisposed);
            this.selfEndpointTracker = selfEndpointTracker;
            this.Requirements = new NetworkPeerRequirement { MinVersion = nodeSettings.MinProtocolVersion ?? nodeSettings.ProtocolVersion };

            this.connectionInterval = this.CalculateConnectionInterval();
        }

        /// <inheritdoc/>
        public void Initialize(IConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;

            this.CurrentParameters = connectionManager.Parameters.Clone();

            this.OnInitialize();
        }

        /// <summary>
        /// Adds a peer to the <see cref="ConnectorPeers"/>.
        /// <para>
        /// This will only happen if the peer successfully handshaked with another.
        /// </para>
        /// </summary>
        /// <param name="peer">Peer to be added.</param>
        private void AddPeer(INetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            this.ConnectorPeers.Add(peer);

            if (this.asyncLoop != null)
                this.asyncLoop.RepeatEvery = this.CalculateConnectionInterval();
        }

        /// <summary>
        /// Removes a given peer from the <see cref="ConnectorPeers"/>.
        /// <para>
        /// This will happen if the peer state changed to "disconnecting", "failed" or "offline".
        /// </para>
        /// </summary>
        /// <param name="peer">Peer to be removed.</param>
        private void RemovePeer(INetworkPeer peer)
        {
            this.ConnectorPeers.Remove(peer);

            if (this.asyncLoop != null)
                this.asyncLoop.RepeatEvery = this.CalculateConnectionInterval();
        }

        /// <summary>Determines whether or not a connector can be started.</summary>
        public abstract bool CanStartConnect { get; }

        /// <summary>Initialization logic specific to each concrete implementation of this class.</summary>
        public abstract void OnInitialize();

        /// <summary>Start up logic specific to each concrete implementation of this class.</summary>
        public abstract void OnStartConnect();

        /// <summary>Connect logic specific to each concrete implementation of this class.</summary>
        public abstract Task OnConnectAsync();

        /// <summary>
        /// <c>true</c> if the peer is already connected.
        /// </summary>
        /// <param name="ipEndpoint">The endpoint to check.</param>
        internal bool IsPeerConnected(IPEndPoint ipEndpoint)
        {
            return this.connectionManager.ConnectedPeers.FindByEndpoint(ipEndpoint) != null;
        }

        /// <inheritdoc/>
        public void StartConnectAsync()
        {
            if (!this.CanStartConnect)
                return;

            this.OnStartConnect();

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                if (!this.peerAddressManager.Peers.Any() || (this.ConnectorPeers.Count >= this.MaxOutboundConnections))
                    return;

                await this.OnConnectAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: this.connectionInterval);
        }

        /// <summary>Attempts to connect to a random peer.</summary>
        internal async Task ConnectAsync(PeerAddress peerAddress)
        {
            if (this.selfEndpointTracker.IsSelf(peerAddress.Endpoint))
            {
                this.logger.LogTrace("{0} is self. Therefore not connecting.", peerAddress.Endpoint);
                return;
            }

            INetworkPeer peer = null;

            try
            {
                using (CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    this.peerAddressManager.PeerAttempted(peerAddress.Endpoint, this.dateTimeProvider.GetUtcNow());

                    NetworkPeerConnectionParameters clonedConnectParamaters = this.CurrentParameters.Clone();
                    timeoutTokenSource.CancelAfter(5000);
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    peer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(peerAddress.Endpoint, clonedConnectParamaters, this.networkPeerDisposer).ConfigureAwait(false);

                    await peer.VersionHandshakeAsync(this.Requirements, timeoutTokenSource.Token).ConfigureAwait(false);
                    this.AddPeer(peer);
                }
            }
            catch (OperationCanceledException)
            {
                if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    this.logger.LogDebug("Peer {0} connection canceled because application is stopping.", peerAddress.Endpoint);
                    peer?.Disconnect("Application stopping");
                }
                else
                {
                    this.logger.LogDebug("Peer {0} connection timeout.", peerAddress.Endpoint);
                    peerAddress.SetHandshakeAttempted(this.dateTimeProvider.GetUtcNow());
                    peer?.Disconnect("Connection timeout");
                }
            }
            catch (NBitcoin.Protocol.ProtocolException)
            {
                this.logger.LogDebug("Handshake rejected by peer '{0}'.", peerAddress.Endpoint);
                peerAddress.SetHandshakeAttempted(this.dateTimeProvider.GetUtcNow());
                peer?.Disconnect("Error while handshaking");
            }
            catch (Exception exception)
            {
                this.logger.LogTrace("Exception occurred while connecting: {0}", exception.ToString());
                peerAddress.SetHandshakeAttempted(this.dateTimeProvider.GetUtcNow());
                peer?.Disconnect("Error while connecting", exception);
            }
        }

        /// <summary>
        /// Determines how often the connector should try and connect to an address from it's list.
        /// </summary>
        [NoTrace]
        public virtual TimeSpan CalculateConnectionInterval()
        {
            return this.ConnectorPeers.Count < this.ConnectionSettings.InitialConnectionTarget ? TimeSpans.Ms100 : TimeSpans.Second;
        }

        /// <summary>
        /// Callback that is called before the peer is disposed.
        /// </summary>
        /// <param name="peer">Peer that is being disposed.</param>
        private void OnPeerDisposed(INetworkPeer peer)
        {
            this.RemovePeer(peer);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.networkPeerDisposer.Dispose();
        }
    }
}
