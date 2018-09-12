using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
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
        private IPeerDiscovery peerDiscovery;

        private readonly NetworkPeerCollection connectedPeers;

        /// <summary>Registry of endpoints used to identify this node.</summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        public IReadOnlyNetworkPeerCollection ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        private NetworkPeerServices discoveredNodeRequiredService = NetworkPeerServices.Network;

        public List<NetworkPeerServer> Servers { get; }

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        private readonly IVersionProvider versionProvider;

        private IConsensusManager consensusManager;

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
            INodeStats nodeStats)
        {
            this.connectedPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = network;
            this.NetworkPeerFactory = networkPeerFactory;
            this.NodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.peerAddressManager = peerAddressManager;
            this.PeerConnectors = peerConnectors;
            this.peerDiscovery = peerDiscovery;
            this.ConnectionSettings = connectionSettings;
            this.networkPeerDisposer = new NetworkPeerDisposer(this.loggerFactory);
            this.Servers = new List<NetworkPeerServer>();

            this.Parameters = parameters;
            this.Parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
            this.selfEndpointTracker = selfEndpointTracker;
            this.versionProvider = versionProvider;

            this.Parameters.UserAgent = $"{this.ConnectionSettings.Agent}:{versionProvider.GetVersion()}";

            this.Parameters.Version = this.NodeSettings.ProtocolVersion;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <inheritdoc />
        public void Initialize(IConsensusManager consensusManager)
        {
            this.logger.LogTrace("()");

            this.consensusManager = consensusManager;
            this.AddExternalIpToSelfEndpoints();

            this.peerDiscovery.DiscoverPeers(this);

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
            {
                peerConnector.Initialize(this);
                peerConnector.StartConnectAsync();
            }

            this.StartNodeServer();

            // If external IP address supplied this overrides all.
            if (this.ConnectionSettings.ExternalEndpoint != null)
            {
                this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(this.ConnectionSettings.ExternalEndpoint, true);
            }
            else
            {
                // If external IP address not supplied take first routable bind address and set score to 10.
                IPEndPoint nodeServerEndpoint = this.ConnectionSettings.Listen?.FirstOrDefault(x => x.Endpoint.Address.IsRoutable(false))?.Endpoint;
                if (nodeServerEndpoint != null)
                {
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(nodeServerEndpoint, false, 10);
                }
                else
                {
                    this.selfEndpointTracker.UpdateAndAssignMyExternalAddress(new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), this.ConnectionSettings.Port), false);
                }
            }

            this.logger.LogTrace("(-)");
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

            foreach (NodeServerEndpoint listen in this.ConnectionSettings.Listen)
            {
                NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
                NetworkPeerServer server = this.NetworkPeerFactory.CreateNetworkPeerServer(listen.Endpoint, this.ConnectionSettings.ExternalEndpoint);

                this.Servers.Add(server);
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(this, this.loggerFactory)
                {
                    Whitelisted = listen.Whitelisted
                });

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
            this.logger.LogTrace("({0}:{1})", nameof(services), services);

            this.discoveredNodeRequiredService |= services;

            IPeerConnector peerConnector = this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorDiscovery);
            if ((peerConnector != null) && !peerConnector.Requirements.RequiredServices.HasFlag(services))
            {
                peerConnector.Requirements.RequiredServices |= NetworkPeerServices.NODE_WITNESS;
                foreach (INetworkPeer peer in peerConnector.ConnectorPeers)
                {
                    if (!peer.PeerVersion.Services.HasFlag(services))
                        peer.Disconnect("The peer does not support the required services requirement.");
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void AddComponentStats(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine($"======Connection====== agent {this.Parameters.UserAgent}");

            foreach (INetworkPeer peer in this.ConnectedPeers)
            {
                var chainHeadersBehavior = peer.Behavior<ConsensusManagerBehavior>();

                string agent = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]";
                builder.AppendLine(
                    "Peer:" + (peer.RemoteSocketEndpoint + ", ").PadRight(LoggingConfiguration.ColumnLength + 15) +
                    (" connected:" + (peer.Inbound ? "inbound" : "outbound") + ",").PadRight(LoggingConfiguration.ColumnLength + 7) +
                    (" height:" + (chainHeadersBehavior.ExpectedPeerTip != null ? chainHeadersBehavior.ExpectedPeerTip.Height.ToString() :
                         peer.PeerVersion?.StartHeight.ToString() ?? "unknown") + ",").PadRight(LoggingConfiguration.ColumnLength + 2) +
                    " agent:" + agent);
            }
        }

        private string ToKBSec(ulong bytesPerSec)
        {
            double speed = ((double)bytesPerSec / 1024.0);
            return speed.ToString("0.00") + " KB/S";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.logger.LogInformation("Stopping peer discovery...");
            this.peerDiscovery?.Dispose();

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
                peerConnector.Dispose();

            foreach (NetworkPeerServer server in this.Servers)
                server.Dispose();

            this.networkPeerDisposer.Dispose();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void AddConnectedPeer(INetworkPeer peer)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(peer), peer.RemoteSocketEndpoint);

            this.connectedPeers.Add(peer);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void RemoveConnectedPeer(INetworkPeer peer, string reason)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(reason), reason);

            this.connectedPeers.Remove(peer);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void PeerDisconnected(int networkPeerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(networkPeerId), networkPeerId);

            this.consensusManager.PeerDisconnected(networkPeerId);

            this.logger.LogTrace("(-)");
        }

        public INetworkPeer FindNodeByEndpoint(IPEndPoint ipEndpoint)
        {
            return this.connectedPeers.FindByEndpoint(ipEndpoint);
        }

        public INetworkPeer FindNodeByIp(IPAddress ipAddress)
        {
            return this.connectedPeers.FindByIp(ipAddress);
        }

        public INetworkPeer FindLocalNode()
        {
            return this.connectedPeers.FindLocal();
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
        public void AddNodeAddress(IPEndPoint ipEndpoint)
        {
            Guard.NotNull(ipEndpoint, nameof(ipEndpoint));

            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);

            if (!this.ConnectionSettings.AddNode.Any(p => p.Match(ipEndpoint)))
            {
                this.ConnectionSettings.AddNode.Add(ipEndpoint);
                this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorAddNode).MaxOutboundConnections++;
            }
            else
                this.logger.LogTrace("The endpoint already exists in the add node collection.");

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            INetworkPeer peer = this.connectedPeers.FindByEndpoint(ipEndpoint);

            if (peer != null)
            {
                peer.Disconnect("Requested by user");
                this.RemoveConnectedPeer(peer, "Requested by user");
            }

            this.peerAddressManager.RemovePeer(ipEndpoint);
            IEnumerable<IPEndPoint> matchingAddNodes = this.ConnectionSettings.AddNode.Where(p => p.Match(ipEndpoint));
            foreach (IPEndPoint m in matchingAddNodes)
                this.ConnectionSettings.AddNode.Remove(m);

            this.logger.LogTrace("(-)");
        }

        public async Task<INetworkPeer> ConnectAsync(IPEndPoint ipEndpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
            cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(this, this.loggerFactory)
            {
                OneTry = true
            });

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

            this.logger.LogTrace("(-)");
            return peer;
        }
    }
}