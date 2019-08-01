using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Connection
{
    public sealed class ConnectionManager : IConnectionManager
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The maximum number of entries in an 'inv' protocol message.</summary>
        public const int MaxInventorySize = 50000;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <inheritdoc/>
        public Network Network { get; private set; }

        /// <inheritdoc/>
        public INetworkPeerFactory NetworkPeerFactory { get; private set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <inheritdoc/>
        public NodeSettings NodeSettings { get; private set; }

        /// <inheritdoc/>
        public ConnectionManagerSettings ConnectionSettings { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerConnectionParameters Parameters { get; }

        /// <inheritdoc/>
        public IEnumerable<IPeerConnector> PeerConnectors { get; private set; }

        /// <summary>Manager class that handles peers and their respective states.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>Async loop that discovers new peers to connect to.</summary>
        private readonly IPeerDiscovery peerDiscovery;

        private readonly List<IPEndPoint> ipRangeFilteringEndpointExclusions;

        private readonly NetworkPeerCollection connectedPeers;

        /// <summary>Registry of endpoints used to identify this node.</summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        public IReadOnlyNetworkPeerCollection ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        /// <inheritdoc/>
        public List<NetworkPeerServer> Servers { get; }

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        private readonly IVersionProvider versionProvider;

        private readonly IAsyncProvider asyncProvider;

        private IConsensusManager consensusManager;

        private readonly IAsyncDelegateDequeuer<INetworkPeer> connectedPeersQueue;

        /// <summary>Traffic statistics from peers that have been disconnected.</summary>
        private readonly PerformanceCounter disconnectedPerfCounter;

        public ConnectionManager(IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            NodeSettings nodeSettings,
            INodeLifetime nodeLifetime,
            NetworkPeerConnectionParameters parameters,
            IPeerAddressManager peerAddressManager,
            IEnumerable<IPeerConnector> peerConnectors,
            IPeerDiscovery peerDiscovery,
            ISelfEndpointTracker selfEndpointTracker,
            ConnectionManagerSettings connectionSettings,
            IVersionProvider versionProvider,
            INodeStats nodeStats,
            IAsyncProvider asyncProvider)
        {
            this.connectedPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = network;
            this.NetworkPeerFactory = networkPeerFactory;
            this.NodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
            this.peerAddressManager = peerAddressManager;
            this.PeerConnectors = peerConnectors;
            this.peerDiscovery = peerDiscovery;
            this.ConnectionSettings = connectionSettings;
            this.networkPeerDisposer = new NetworkPeerDisposer(this.loggerFactory, this.asyncProvider);
            this.Servers = new List<NetworkPeerServer>();

            this.Parameters = parameters;
            this.Parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
            this.selfEndpointTracker = selfEndpointTracker;
            this.versionProvider = versionProvider;
            this.ipRangeFilteringEndpointExclusions = new List<IPEndPoint>();
            this.connectedPeersQueue = asyncProvider.CreateAndRunAsyncDelegateDequeuer<INetworkPeer>($"{nameof(ConnectionManager)}-{nameof(this.connectedPeersQueue)}", this.OnPeerAdded);
            this.disconnectedPerfCounter = new PerformanceCounter();

            this.Parameters.UserAgent = $"{this.ConnectionSettings.Agent}:{versionProvider.GetVersion()} ({(int)this.NodeSettings.ProtocolVersion})";

            this.Parameters.Version = this.NodeSettings.ProtocolVersion;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name, 1100);
        }

        /// <inheritdoc />
        public void Initialize(IConsensusManager consensusManager)
        {
            this.consensusManager = consensusManager;
            this.AddExternalIpToSelfEndpoints();

            if (this.ConnectionSettings.Listen)
                this.peerDiscovery.DiscoverPeers(this);

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
            {
                peerConnector.Initialize(this);
                peerConnector.StartConnectAsync();
            }

            if (this.ConnectionSettings.Listen)
                this.StartNodeServer();

            // If external IP address supplied this overrides all.
            if (this.ConnectionSettings.ExternalEndpoint != null)
            {
                if (this.ConnectionSettings.ExternalEndpoint.Address.Equals(IPAddress.Loopback))
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(this.ConnectionSettings.ExternalEndpoint, false);
                else
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(this.ConnectionSettings.ExternalEndpoint, true);
            }
            else
            {
                // If external IP address not supplied take first routable bind address and set score to 10.
                IPEndPoint nodeServerEndpoint = this.ConnectionSettings.Bind?.FirstOrDefault(x => x.Endpoint.Address.IsRoutable(false))?.Endpoint;
                if (nodeServerEndpoint != null)
                {
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(nodeServerEndpoint, false, 10);
                }
                else
                {
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), this.ConnectionSettings.Port), false);
                }
            }
        }

        /// <summary>
        /// If -externalip was set on startup, put it in the registry of known selves so
        /// we can avoid connecting to our own node.
        /// </summary>
        private void AddExternalIpToSelfEndpoints()
        {
            if (this.ConnectionSettings.ExternalEndpoint != null)
                this.selfEndpointTracker.Add(this.ConnectionSettings.ExternalEndpoint);
        }

        private void StartNodeServer()
        {
            var logs = new StringBuilder();
            logs.AppendLine("Node listening on:");

            foreach (NodeServerEndpoint listen in this.ConnectionSettings.Bind)
            {
                NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
                NetworkPeerServer server = this.NetworkPeerFactory.CreateNetworkPeerServer(listen.Endpoint, this.ConnectionSettings.ExternalEndpoint, this.Parameters.Version);

                this.Servers.Add(server);
                var cmb = (cloneParameters.TemplateBehaviors.Single(x => x is IConnectionManagerBehavior) as ConnectionManagerBehavior);
                cmb.Whitelisted = listen.Whitelisted;

                server.InboundNetworkPeerConnectionParameters = cloneParameters;
                try
                {
                    server.Listen();
                }
                catch (SocketException e)
                {
                    this.logger.LogCritical("Unable to listen on port {0} (you can change the port using '-port=[number]'). Error message: {1}", listen.Endpoint.Port, e.Message);
                    throw e;
                }

                logs.Append(listen.Endpoint.Address + ":" + listen.Endpoint.Port);
                if (listen.Whitelisted)
                    logs.Append(" (whitelisted)");

                logs.AppendLine();
            }

            this.logger.LogInformation(logs.ToString());
        }

        public void AddDiscoveredNodesRequirement(NetworkPeerServices services)
        {
            IPeerConnector peerConnector = this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorDiscovery);
            if ((peerConnector != null) && !peerConnector.Requirements.RequiredServices.HasFlag(services))
            {
                peerConnector.Requirements.RequiredServices |= services;
                foreach (INetworkPeer peer in peerConnector.ConnectorPeers)
                {
                    if (peer.Inbound) continue;

                    if (!peer.PeerVersion.Services.HasFlag(services))
                        peer.Disconnect("The peer does not support the required services requirement.");
                }
            }
        }

        private void AddComponentStats(StringBuilder builder)
        {
            // The total traffic will be the sum of the disconnected peers' traffic and the currently connected peers' traffic.
            long totalRead = this.disconnectedPerfCounter.ReadBytes;
            long totalWritten = this.disconnectedPerfCounter.WrittenBytes;

            void AddPeerInfo(StringBuilder peerBuilder, INetworkPeer peer)
            {
                var chainHeadersBehavior = peer.Behavior<ConsensusManagerBehavior>();
                var connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();

                string peerHeights = $"(r/s/c):" +
                                     $"{(chainHeadersBehavior.BestReceivedTip != null ? chainHeadersBehavior.BestReceivedTip.Height.ToString() : peer.PeerVersion != null ? peer.PeerVersion.StartHeight + "*" : "-")}" +
                                     $"/{(chainHeadersBehavior.BestSentHeader != null ? chainHeadersBehavior.BestSentHeader.Height.ToString() : peer.PeerVersion != null ? peer.PeerVersion.StartHeight + "*" : "-")}" +
                                     $"/{chainHeadersBehavior.GetCachedItemsCount()}";

                string peerTraffic = $"R/S MB: {peer.Counter.ReadBytes.BytesToMegaBytes()}/{peer.Counter.WrittenBytes.BytesToMegaBytes()}";
                totalRead += peer.Counter.ReadBytes;
                totalWritten += peer.Counter.WrittenBytes;

                string agent = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]";
                peerBuilder.AppendLine(
                    (peer.Inbound ? "IN  " : "OUT ") + "Peer:" + (peer.RemoteSocketEndpoint + ", ").PadRight(LoggingConfiguration.ColumnLength + 15)
                    + peerHeights.PadRight(LoggingConfiguration.ColumnLength + 14)
                    + peerTraffic.PadRight(LoggingConfiguration.ColumnLength + 7)
                    + " agent:" + agent);
            }

            var oneTryBuilder = new StringBuilder();
            var whiteListedBuilder = new StringBuilder();
            var addNodeBuilder = new StringBuilder();
            var connectBuilder = new StringBuilder();
            var otherBuilder = new StringBuilder();
            var addNodeDict = this.ConnectionSettings.AddNode.ToDictionary(ep => ep.MapToIpv6(), ep => ep);
            var connectDict = this.ConnectionSettings.Connect.ToDictionary(ep => ep.MapToIpv6(), ep => ep);

            foreach (INetworkPeer peer in this.ConnectedPeers)
            {
                bool added = false;

                var connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                if (connectionManagerBehavior.OneTry)
                {
                    AddPeerInfo(oneTryBuilder, peer);
                    added = true;
                }

                if (connectionManagerBehavior.Whitelisted)
                {
                    AddPeerInfo(whiteListedBuilder, peer);
                    added = true;
                }

                if (connectDict.ContainsKey(peer.PeerEndPoint))
                {
                    AddPeerInfo(connectBuilder, peer);
                    added = true;
                }

                if (addNodeDict.ContainsKey(peer.PeerEndPoint))
                {
                    AddPeerInfo(addNodeBuilder, peer);
                    added = true;
                }

                if (!added)
                {
                    AddPeerInfo(otherBuilder, peer);
                }
            }

            int inbound = this.ConnectedPeers.Count(x => x.Inbound);

            builder.AppendLine();
            builder.AppendLine($"======Connection====== agent {this.Parameters.UserAgent} [in:{inbound} out:{this.ConnectedPeers.Count() - inbound}] [recv: {totalRead.BytesToMegaBytes()} MB sent: {totalWritten.BytesToMegaBytes()} MB]");

            if (whiteListedBuilder.Length > 0)
            {
                builder.AppendLine(">>> Whitelisted:");
                builder.Append(whiteListedBuilder.ToString());
                builder.AppendLine("<<<");
            }

            if (addNodeBuilder.Length > 0)
            {
                builder.AppendLine(">>> AddNode:");
                builder.Append(addNodeBuilder.ToString());
                builder.AppendLine("<<<");
            }

            if (oneTryBuilder.Length > 0)
            {
                builder.AppendLine(">>> OneTry:");
                builder.Append(oneTryBuilder.ToString());
                builder.AppendLine("<<<");
            }

            if (connectBuilder.Length > 0)
            {
                builder.AppendLine(">>> Connect:");
                builder.Append(connectBuilder.ToString());
                builder.AppendLine("<<<");
            }

            if (otherBuilder.Length > 0)
                builder.Append(otherBuilder.ToString());
        }

        private string ToKBSec(ulong bytesPerSec)
        {
            double speed = ((double)bytesPerSec / 1024.0);
            return speed.ToString("0.00") + " KB/S";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogInformation("Stopping peer discovery.");
            this.peerDiscovery?.Dispose();

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
                peerConnector.Dispose();

            foreach (NetworkPeerServer server in this.Servers)
                server.Dispose();

            this.networkPeerDisposer.Dispose();
        }

        /// <inheritdoc />
        public void AddConnectedPeer(INetworkPeer peer)
        {
            this.connectedPeers.Add(peer);
            this.connectedPeersQueue.Enqueue(peer);
        }

        private Task OnPeerAdded(INetworkPeer peer, CancellationToken cancellationToken)
        {
            // Code in this method is a quick and dirty fix for the race condition described here: https://github.com/stratisproject/StratisBitcoinFullNode/issues/2864
            // TODO race condition should be eliminated instead of fixing its consequences.

            if (this.ShouldDisconnect(peer))
                peer.Disconnect("Peer from the same network group.");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines if the peer should be disconnected.
        /// Peer should be disconnected in case it's IP is from the same group in which any other peer
        /// is and the peer wasn't added using -connect or -addNode command line arguments.
        /// </summary>
        private bool ShouldDisconnect(INetworkPeer peer)
        {
            // Don't disconnect if range filtering is not turned on.
            if (!this.ConnectionSettings.IpRangeFiltering)
            {
                this.logger.LogTrace("(-)[IP_RANGE_FILTERING_OFF]:false");
                return false;
            }

            // Don't disconnect if this peer has a local host address.
            if (peer.PeerEndPoint.Address.IsLocal())
            {
                this.logger.LogTrace("(-)[IP_IS_LOCAL]:false");
                return false;
            }

            // Don't disconnect if this peer is in -addnode or -connect.
            if (this.ConnectionSettings.AddNode.Union(this.ConnectionSettings.Connect).Any(ep => peer.PeerEndPoint.MatchIpOnly(ep)))
            {
                this.logger.LogTrace("(-)[ADD_NODE_OR_CONNECT]:false");
                return false;
            }

            // Don't disconnect if this peer is in the exclude from IP range filtering group.
            if (this.ipRangeFilteringEndpointExclusions.Any(ip => ip.MatchIpOnly(peer.PeerEndPoint)))
            {
                this.logger.LogTrace("(-)[PEER_IN_IPRANGEFILTER_EXCLUSIONS]:false");
                return false;
            }

            byte[] peerGroup = peer.PeerEndPoint.MapToIpv6().Address.GetGroup();

            foreach (INetworkPeer connectedPeer in this.ConnectedPeers)
            {
                if (peer == connectedPeer)
                    continue;

                byte[] group = connectedPeer.PeerEndPoint.MapToIpv6().Address.GetGroup();

                if (peerGroup.SequenceEqual(group))
                {
                    this.logger.LogTrace("(-)[SAME_GROUP]:true");
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public void RemoveConnectedPeer(INetworkPeer peer, string reason)
        {
            this.connectedPeers.Remove(peer);
            this.disconnectedPerfCounter.Add(peer.Counter);
        }

        /// <inheritdoc />
        public void PeerDisconnected(int networkPeerId)
        {
            this.consensusManager.PeerDisconnected(networkPeerId);
        }

        public INetworkPeer FindNodeByEndpoint(IPEndPoint ipEndpoint)
        {
            return this.connectedPeers.FindByEndpoint(ipEndpoint);
        }

        public INetworkPeer FindNodeById(int peerId)
        {
            return this.connectedPeers.FindById(peerId);
        }

        /// <summary>
        /// Adds a node to the -addnode collection.
        /// <para>
        /// Usually called via RPC.
        /// </para>
        /// </summary>
        /// <param name="ipEndpoint">The endpoint of the peer to add.</param>
        public void AddNodeAddress(IPEndPoint ipEndpoint, bool excludeFromIpRangeFiltering = false)
        {
            Guard.NotNull(ipEndpoint, nameof(ipEndpoint));

            if (excludeFromIpRangeFiltering && !this.ipRangeFilteringEndpointExclusions.Any(ip => ip.Match(ipEndpoint)))
            {
                this.logger.LogDebug("{0} will be excluded from IP range filtering.", ipEndpoint);
                this.ipRangeFilteringEndpointExclusions.Add(ipEndpoint);
            }

            this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);

            if (!this.ConnectionSettings.AddNode.Any(p => p.Match(ipEndpoint)))
            {
                this.ConnectionSettings.AddNode.Add(ipEndpoint);
                this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorAddNode).MaxOutboundConnections++;
            }
            else
                this.logger.LogDebug("The endpoint already exists in the add node collection.");
        }

        /// <summary>
        /// Disconnect a peer.
        /// <para>
        /// Usually called via RPC.
        /// </para>
        /// </summary>
        /// <param name="ipEndpoint">The endpoint of the peer to disconnect.</param>
        public void RemoveNodeAddress(IPEndPoint ipEndpoint)
        {
            INetworkPeer peer = this.connectedPeers.FindByEndpoint(ipEndpoint);

            if (peer != null)
            {
                peer.Disconnect("Requested by user");
                this.RemoveConnectedPeer(peer, "Requested by user");
            }

            this.peerAddressManager.RemovePeer(ipEndpoint);

            // There appears to be a race condition that causes the endpoint or endpoint's address property to be null when
            // trying to remove it from the connection manager's add node collection.
            if (ipEndpoint == null)
            {
                this.logger.LogTrace("(-)[IPENDPOINT_NULL]");
                throw new ArgumentNullException(nameof(ipEndpoint));
            }

            if (ipEndpoint.Address == null)
            {
                this.logger.LogTrace("(-)[IPENDPOINT_ADDRESS_NULL]");
                throw new ArgumentNullException(nameof(ipEndpoint.Address));
            }

            if (this.ConnectionSettings.AddNode.Any(ip => ip == null))
            {
                this.logger.LogTrace("(-)[ADDNODE_CONTAINS_NULLS]");
                throw new ArgumentNullException("The addnode collection contains null entries.");
            }

            foreach (var endpoint in this.ConnectionSettings.AddNode.Where(a => a.Address == null))
            {
                this.logger.LogTrace("(-)[IPENDPOINT_ADDRESS_NULL]:{0}", endpoint);
                throw new ArgumentNullException("The addnode collection contains endpoints with null addresses.");
            }

            // Create a copy of the nodes to remove. This avoids errors due to both modifying the collection and iterating it.
            List<IPEndPoint> matchingAddNodes = this.ConnectionSettings.AddNode.Where(p => p.Match(ipEndpoint)).ToList();
            foreach (IPEndPoint m in matchingAddNodes)
                this.ConnectionSettings.AddNode.Remove(m);
        }

        public async Task<INetworkPeer> ConnectAsync(IPEndPoint ipEndpoint)
        {
            var existingConnection = this.connectedPeers.FirstOrDefault(connectedPeer => connectedPeer.PeerEndPoint.Match(ipEndpoint));

            if (existingConnection != null)
            {
                this.logger.LogDebug("{0} is already connected.");
                return existingConnection;
            }

            NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();

            var connectionManagerBehavior = cloneParameters.TemplateBehaviors.OfType<ConnectionManagerBehavior>().SingleOrDefault();
            if (connectionManagerBehavior != null)
            {
                connectionManagerBehavior.OneTry = true;
            }

            INetworkPeer peer = await this.NetworkPeerFactory.CreateConnectedNetworkPeerAsync(ipEndpoint, cloneParameters, this.networkPeerDisposer).ConfigureAwait(false);

            try
            {
                this.peerAddressManager.PeerAttempted(ipEndpoint, this.dateTimeProvider.GetUtcNow());
                await peer.VersionHandshakeAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                peer.Disconnect("Connection failed");
                this.logger.LogTrace("(-)[ERROR]");
                throw e;
            }

            return peer;
        }
    }
}