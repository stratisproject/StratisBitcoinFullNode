using System;
using System.Linq;
using System.Net;
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
        NodesCollection ConnectedPeers { get; }

        /// <summary>
        /// Other peer connectors this instance relates. 
        /// <para>
        /// This is used to ensure that the same IP doesn't get connected to in this connector.
        /// </para>
        /// </summary>
        RelatedPeerConnectors RelatedPeerConnector { get; set; }

        /// <summary>Specification of requirements the <see cref="PeerConnector"/> has when connecting to other peers.</summary>
        NodeRequirement Requirements { get; }

        /// <summary>
        /// Adds a node to the <see cref="NodesCollection"/>.
        /// <para>
        /// This will only happen if the node successfully handshaked with another.
        /// </para>
        /// </summary>
        void AddNode(Node node);

        /// <summary>
        /// Removes a given node from the <see cref="NodesCollection"/>.
        /// <para>
        /// This will happen if the node state changed to "disconnecting", "failed" or "offline".
        /// </para>
        /// </summary>
        void RemoveNode(Node node);

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
        public NodesCollection ConnectedPeers { get; private set; }

        /// <summary>The cloned parameters used to connect to peers. </summary>
        private readonly NetworkPeerConnectionParameters currentParameters;

        /// <summary>How to calculate a group of an IP, by default using NBitcoin.IpExtensions.GetGroup.</summary>
        private readonly Func<IPEndPoint, byte[]> groupSelector;

        /// <inheritdoc/>
        public int MaximumNodeConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>The node parameters that is injected by <see cref="Connection.ConnectionManager"/>.</summary>
        private readonly NodeConnectionParameters parentParameters;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>What peer types (by <see cref="PeerIntroductionType"/> this connector should find and connect to.</summary>
        private readonly PeerIntroductionType peerIntroductionType;

        /// <inheritdoc/>
        public RelatedPeerConnectors RelatedPeerConnector { get; set; }

        /// <inheritdoc/>
        public NodeRequirement Requirements { get; private set; }

        /// <summary>Constructor used for unit testing.</summary>
        internal PeerConnector(
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.nodeLifetime = new NodeLifetime();
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
            this.RelatedPeerConnector = new RelatedPeerConnectors();
        }

        /// <summary>Constructor used by dependency injection.</summary>
        internal PeerConnector(Network network,
            INodeLifetime nodeLifeTime,
            NetworkPeerConnectionParameters parameters,
            NodeRequirement nodeRequirements,
            Func<IPEndPoint, byte[]> groupSelector,
            IAsyncLoopFactory asyncLoopFactory,
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedPeers = new NetworkPeerCollection();
            this.groupSelector = groupSelector;
            this.MaximumNodeConnections = 8;
            this.network = network;
            this.nodeLifetime = nodeLifeTime;
            this.parentParameters = parameters;
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
            this.Requirements = nodeRequirements;

            this.currentParameters = this.parentParameters.Clone();
            this.currentParameters.TemplateBehaviors.Add(new PeerConnectorBehaviour(this));
            this.currentParameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
        }

        /// <inheritdoc/>
        public void AddNode(Node node)
        {
            Guard.NotNull(node, nameof(node));

            this.ConnectedPeers.Add(node);
        }

        /// <inheritdoc/>
        public void RemoveNode(Node node)
        {
            this.ConnectedPeers.Remove(node);
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
            NetworkPeer node = null;

            try
            {
                var peer = this.FindPeerToConnectTo();
                if (peer == null)
                    return Task.CompletedTask;

                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    timeoutTokenSource.CancelAfter(5000);

                    this.peerAddressManager.PeerAttempted(peer.Endpoint, DateTimeProvider.Default.GetUtcNow());

                    var clonedConnectParamaters = this.currentParameters.Clone();
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    node = NetworkPeer.Connect(this.network, peer, clonedConnectParamaters);
                    node.VersionHandshake(this.Requirements, timeoutTokenSource.Token);

                    return Task.CompletedTask;
                }
            }
            catch (Exception exception)
            {
                if (node != null)
                    node.DisconnectAsync("Error while connecting", exception);
            }

            return Task.CompletedTask;
        }

        /// <summary>Disconnects all the nodes in <see cref="ConnectedPeers"/>.</summary>
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
            int groupFail = 0;

            NetworkAddress peer = null;

            while (groupFail < 50 && !this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                peer = this.peerAddressManager.SelectPeerToConnectTo(this.peerIntroductionType);
                if (peer == null)
                    break;

                if (!peer.Endpoint.Address.IsValid())
                    continue;

                var nodeExistsInGroup = this.RelatedPeerConnector.GlobalConnectedNodes().Any(a => this.groupSelector(a).SequenceEqual(this.groupSelector(peer.Endpoint)));
                if (nodeExistsInGroup)
                {
                    groupFail++;
                    continue;
                }

                break;
            }

            return peer;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.Disconnect();
        }
    }
}