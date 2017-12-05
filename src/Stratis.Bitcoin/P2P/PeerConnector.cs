using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Contract for <see cref="PeerConnector"/></summary>
    public interface IPeerConnector : IDisposable
    {
        /// <summary>The collection of peers the node is currently connected to.</summary>
        NetworkPeerCollection ConnectedPeers { get; }

        /// <summary>
        /// Selects a peer from the address manager.
        /// <para>
        /// Refer to <see cref="IPeerAddressManager.SelectPeerToConnectTo()"/> for details on how this is done.
        /// </para>
        /// </summary>
        NetworkAddress FindPeerToConnectTo();

        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        int MaximumNodeConnections { get; set; }

        /// <summary>
        /// Other peer connectors this instance relates. 
        /// <para>
        /// This is used to ensure that the same IP doesn't get connected to in this connector.
        /// </para>
        /// </summary>
        RelatedPeerConnectors RelatedPeerConnector { get; set; }

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
        void StartConnectAsync(NetworkPeerConnectionParameters parameters, PeerAddressManagerBehaviourMode mode);
    }

    /// <summary>
    /// Connects to peers asynchronously, filtered by <see cref="PeerIntroductionType"/>.
    /// </summary>
    public abstract class PeerConnector : IPeerConnector
    {
        /// <summary>The async loop we need to wait upon before we can dispose of this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <inheritdoc/>
        public NetworkPeerCollection ConnectedPeers { get; private set; }

        /// <summary>The cloned parameters used to connect to peers. </summary>
        public NetworkPeerConnectionParameters CurrentParameters { get; private set; }

        /// <summary>How to calculate a group of an IP, by default using NBitcoin.IpExtensions.GetGroup.</summary>
        public Func<IPEndPoint, byte[]> GroupSelector { get; internal set; }

        /// <summary>Instance logger.</summary>
        public readonly ILogger Logger;

        /// <inheritdoc/>
        public int MaximumNodeConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        protected readonly INodeLifetime nodeLifetime;

        /// <summary>User defined node settings.</summary>
        public readonly NodeSettings NodeSettings;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>The network peer parameters that is injected by <see cref="Connection.ConnectionManager"/>.</summary>
        private NetworkPeerConnectionParameters parentParameters;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        protected readonly IPeerAddressManager peerAddressManager;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <inheritdoc/>
        public RelatedPeerConnectors RelatedPeerConnector { get; set; }

        /// <inheritdoc/>
        public NetworkPeerRequirement Requirements { get; internal set; }

        /// <summary>Constructor used for unit testing.</summary>
        protected PeerConnector(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.peerAddressManager = peerAddressManager;
            this.NodeSettings = nodeSettings;
            this.nodeLifetime = new NodeLifetime();
            this.RelatedPeerConnector = new RelatedPeerConnectors();
        }

        /// <summary>Constructor used by <see cref="Connection.ConnectionManager"/>.</summary>
        protected PeerConnector(
            IAsyncLoopFactory asyncLoopFactory,
            ILogger logger,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifeTime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedPeers = new NetworkPeerCollection();
            this.network = network;
            this.networkPeerFactory = networkPeerFactory;
            this.nodeLifetime = nodeLifeTime;
            this.NodeSettings = nodeSettings;
            this.peerAddressManager = peerAddressManager;
        }

        /// <inheritdoc/>
        public void AddPeer(NetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            this.ConnectedPeers.Add(peer);
        }

        /// <inheritdoc/>
        public void RemovePeer(NetworkPeer peer)
        {
            this.ConnectedPeers.Remove(peer);
        }

        internal bool IsPeerConnected(IPEndPoint endPoint)
        {
            bool peerIsConnected = this.RelatedPeerConnector.GlobalConnectedNodes().Any(a => this.GroupSelector(a).SequenceEqual(this.GroupSelector(endPoint)));
            return peerIsConnected;
        }

        /// <inheritdoc/>
        public void StartConnectAsync(NetworkPeerConnectionParameters parameters, PeerAddressManagerBehaviourMode mode)
        {
            this.parentParameters = parameters;

            this.CurrentParameters = this.parentParameters.Clone();
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = mode;
            this.CurrentParameters.TemplateBehaviors.Add(new PeerConnectorBehaviour(this));
            this.CurrentParameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                await this.ConnectAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second);
        }

        /// <summary>Attempts to connect to a random peer.</summary>
        private Task ConnectAsync()
        {
            if (!this.peerAddressManager.Peers.Any())
                return Task.CompletedTask;

            if (this.ConnectedPeers.Count >= this.MaximumNodeConnections)
                return Task.CompletedTask;

            NetworkPeer peer = null;

            try
            {
                NetworkAddress peerAddress = this.FindPeerToConnectTo();
                if (peerAddress == null)
                    return Task.CompletedTask;

                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    timeoutTokenSource.CancelAfter(5000);

                    this.peerAddressManager.PeerAttempted(peerAddress.Endpoint, DateTimeProvider.Default.GetUtcNow());

                    var clonedConnectParamaters = this.CurrentParameters.Clone();
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    peer = this.networkPeerFactory.CreateConnectedNetworkPeer(this.network, peerAddress, clonedConnectParamaters);
                    peer.VersionHandshake(this.Requirements, timeoutTokenSource.Token);

                    return Task.CompletedTask;
                }
            }
            catch (Exception exception)
            {
                if (peer != null)
                    peer.DisconnectWithException("Error while connecting", exception);
            }

            return Task.CompletedTask;
        }

        /// <summary>Disconnects all the peers in <see cref="ConnectedPeers"/>.</summary>
        private void Disconnect()
        {
            this.ConnectedPeers.DisconnectAll();
        }

        /// <inheritdoc/>
        public abstract NetworkAddress FindPeerToConnectTo();

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.Disconnect();
        }
    }
}