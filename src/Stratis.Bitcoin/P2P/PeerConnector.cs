using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Contract for <see cref="PeerConnector"/>
    /// </summary>
    public interface IPeerConnector : IDisposable
    {
        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        int MaximumNodeConnections { get; set; }

        /// <summary>The collection of peers the node is currently connected to.</summary>
        NetworkPeerCollection ConnectedPeers { get; }

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
    /// Connects to peers asynchronously, filtered by <see cref="PeerIntroductionType"/>.
    /// </summary>
    public sealed class PeerConnector : IPeerConnector
    {
        /// <summary>The async loop we need to wait upon before we can dispose of this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <inheritdoc/>
        public NetworkPeerCollection ConnectedPeers { get; private set; }

        /// <summary>The cloned parameters used to connect to peers. </summary>
        private readonly NetworkPeerConnectionParameters currentParameters;

        /// <inheritdoc/>
        public int MaximumNodeConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>The network peer parameters that is injected by <see cref="Connection.ConnectionManager"/>.</summary>
        private readonly NetworkPeerConnectionParameters parentParameters;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>What peer types (by <see cref="PeerIntroductionType"/> this connector should find and connect to.</summary>
        private readonly PeerIntroductionType peerIntroductionType;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        /// <inheritdoc/>
        public NetworkPeerRequirement Requirements { get; private set; }

        /// <summary>Constructor used for unit testing.</summary>
        internal PeerConnector(
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.nodeLifetime = new NodeLifetime();
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
        }

        /// <summary>Constructor used by dependency injection.</summary>
        internal PeerConnector(Network network,
            INodeLifetime nodeLifeTime,
            NetworkPeerConnectionParameters parameters,
            NetworkPeerRequirement requirements,
            IAsyncLoopFactory asyncLoopFactory,
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType,
            INetworkPeerFactory networkPeerFactory)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedPeers = new NetworkPeerCollection();
            this.MaximumNodeConnections = 8;
            this.network = network;
            this.nodeLifetime = nodeLifeTime;
            this.parentParameters = parameters;
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
            this.Requirements = requirements;
            this.networkPeerFactory = networkPeerFactory;

            this.currentParameters = this.parentParameters.Clone();
            this.currentParameters.TemplateBehaviors.Add(new PeerConnectorBehaviour(this));
            this.currentParameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
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

        /// <inheritdoc/>
        public void StartConnectAsync()
        {
            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                if (this.ConnectedPeers.Count < this.MaximumNodeConnections)
                    await this.ConnectAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second);
        }

        /// <summary>Attempts to connect to a random peer.</summary>
        private Task ConnectAsync()
        {
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

                    var clonedConnectParamaters = this.currentParameters.Clone();
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    peer = this.networkPeerFactory.CreateConnectedNetworkPeer(this.network, peerAddress, clonedConnectParamaters);
                    peer.VersionHandshake(this.Requirements, timeoutTokenSource.Token);

                    return Task.CompletedTask;
                }
            }
            catch (Exception exception)
            {
                if (peer != null)
                    peer.DisconnectAsync("Error while connecting", exception);
            }

            return Task.CompletedTask;
        }

        /// <summary>Disconnects all the peers in <see cref="ConnectedPeers"/>.</summary>
        private void Disconnect()
        {
            this.ConnectedPeers.DisconnectAll();
        }

        /// <summary>
        /// Selects a peer from the address manager.
        /// <para>
        /// Refer to <see cref="IPeerAddressManager.SelectPeerToConnectTo(PeerIntroductionType)"/> for details on how this is done.
        /// </para>
        /// </summary>
        internal NetworkAddress FindPeerToConnectTo()
        {
            NetworkAddress peer = this.peerAddressManager.SelectPeerToConnectTo(this.peerIntroductionType);
            if (peer != null && peer.Endpoint.Address.IsValid())
                return peer;
            return null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.Disconnect();
        }
    }
}