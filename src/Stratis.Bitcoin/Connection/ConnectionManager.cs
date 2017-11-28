using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public interface IConnectionManager : IDisposable
    {
        /// <summary>Used when the -addnode argument is passed when running the node.</summary>
        IPeerConnector AddNodePeerConnector { get; }

        IReadOnlyNetworkPeerCollection ConnectedNodes { get; }

        /// <summary>Used when the -connect argument is passed when running the node.</summary>
        IPeerConnector ConnectNodePeerConnector { get; }

        /// <summary>Used when peer discovery takes place.</summary>
        IPeerConnector DiscoverNodesPeerConnector { get; }

        /// <summary>The network the node is running on.</summary>
        Network Network { get; }

        NodeSettings NodeSettings { get; }

        NetworkPeerConnectionParameters Parameters { get; }

        List<NetworkPeerServer> Servers { get; }

        void AddDiscoveredNodesRequirement(NetworkPeerServices services);

        void AddNodeAddress(IPEndPoint endpoint);

        NetworkPeer Connect(IPEndPoint endpoint);

        NetworkPeer FindLocalNode();

        NetworkPeer FindNodeByEndpoint(IPEndPoint endpoint);

        NetworkPeer FindNodeByIp(IPAddress ip);

        string GetNodeStats();

        string GetStats();

        void RemoveNodeAddress(IPEndPoint endpoint);

        void Start();
    }

    public sealed class ConnectionManager : IConnectionManager
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Manager class that handle peers and their respective states.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>Loop that discovers peers to connect to.</summary>
        private PeerDiscoveryLoop peerDiscoveryLoop;

        /// <summary>The maximum number of entries in an 'inv' protocol message.</summary>
        public const int MaxInventorySize = 50000;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Factory for creating P2P network peer clients and servers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        private readonly NetworkPeerCollection connectedNodes = new NetworkPeerCollection();

        public IReadOnlyNetworkPeerCollection ConnectedNodes
        {
            get { return this.connectedNodes; }
        }

        private readonly Dictionary<NetworkPeer, PerformanceSnapshot> downloads = new Dictionary<NetworkPeer, PerformanceSnapshot>();

        private NetworkPeerServices discoveredNodeRequiredService = NetworkPeerServices.Network;

        private readonly ConnectionManagerSettings connectionManagerSettings;

        /// <inheritdoc/>
        public Network Network { get; }

        public NetworkPeerConnectionParameters Parameters { get; }

        public NodeSettings NodeSettings { get; }

        public List<NetworkPeerServer> Servers { get; }

        /// <inheritdoc/>
        public IPeerConnector AddNodePeerConnector { get; private set; }

        /// <inheritdoc/>
        public IPeerConnector ConnectNodePeerConnector { get; private set; }

        /// <inheritdoc/>
        public IPeerConnector DiscoverNodesPeerConnector { get; private set; }

        public ConnectionManager(
            Network network,
            NetworkPeerConnectionParameters parameters,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            IPeerAddressManager peerAddressManager,
            IDateTimeProvider dateTimeProvider,
            INetworkPeerFactory networkPeerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.Network = network;
            this.NodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.connectionManagerSettings = nodeSettings.ConnectionManager;
            this.Parameters = parameters;
            this.Parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.asyncLoopFactory = asyncLoopFactory;

            this.Servers = new List<NetworkPeerServer>();

            this.peerAddressManager = peerAddressManager;
            this.networkPeerFactory = networkPeerFactory;
        }

        public void Start()
        {
            this.logger.LogTrace("()");

            this.Parameters.UserAgent = $"{this.NodeSettings.Agent}:{this.GetVersion()}";
            this.Parameters.Version = this.NodeSettings.ProtocolVersion;

            NetworkPeerConnectionParameters clonedParameters = this.Parameters.Clone();
            clonedParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory));

            // Don't start peer discovery if we have specified any nodes using the -connect arg.
            if (!this.connectionManagerSettings.Connect.Any())
            {
                if (this.Parameters.PeerAddressManagerBehaviour().Mode.HasFlag(PeerAddressManagerBehaviourMode.Discover))
                {
                    this.logger.LogInformation("Starting peer discovery...");

                    this.peerDiscoveryLoop = new PeerDiscoveryLoop(this.asyncLoopFactory, this.Network, clonedParameters, this.nodeLifetime, this.peerAddressManager, this.networkPeerFactory);
                    this.peerDiscoveryLoop.DiscoverPeers();
                }

                this.DiscoverNodesPeerConnector = this.CreatePeerConnector(clonedParameters, this.discoveredNodeRequiredService, WellKnownPeerConnectorSelectors.ByNetwork, PeerIntroductionType.Discover);
            }
            else
            {
                // Use if we have specified any nodes using the -connect arg
                var peers = this.connectionManagerSettings.Connect.Select(node => new NetworkAddress(node)).ToArray();
                this.peerAddressManager.AddPeers(peers, IPAddress.Loopback, PeerIntroductionType.Connect);
                clonedParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;

                this.ConnectNodePeerConnector = this.CreatePeerConnector(clonedParameters, NetworkPeerServices.Nothing, WellKnownPeerConnectorSelectors.ByEndpoint, PeerIntroductionType.Connect, this.connectionManagerSettings.Connect.Count);
            }

            {
                // Use if we have specified any nodes using the -addnode arg
                var peers = this.connectionManagerSettings.AddNode.Select(node => new NetworkAddress(node)).ToArray();
                this.peerAddressManager.AddPeers(peers, IPAddress.Loopback, PeerIntroductionType.Add);
                clonedParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;

                this.AddNodePeerConnector = this.CreatePeerConnector(clonedParameters, NetworkPeerServices.Nothing, WellKnownPeerConnectorSelectors.ByEndpoint, PeerIntroductionType.Add, this.connectionManagerSettings.AddNode.Count);
            }

            // Relate the peer connectors to each other to prevent duplicate connections.
            var relatedPeerConnectors = new RelatedPeerConnectors();
            relatedPeerConnectors.Register("Discovery", this.DiscoverNodesPeerConnector);
            relatedPeerConnectors.Register("Connect", this.ConnectNodePeerConnector);
            relatedPeerConnectors.Register("AddNode", this.AddNodePeerConnector);

            this.DiscoverNodesPeerConnector?.StartConnectAsync();
            this.ConnectNodePeerConnector?.StartConnectAsync();
            this.AddNodePeerConnector?.StartConnectAsync();

            this.StartNodeServer();

            this.logger.LogTrace("(-)");
        }

        private void StartNodeServer()
        {
            var logs = new StringBuilder();
            logs.AppendLine("Node listening on:");

            foreach (NodeServerEndpoint listen in this.connectionManagerSettings.Listen)
            {
                NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
                NetworkPeerServer server = this.networkPeerFactory.CreateNetworkPeerServer(this.Network);
                server.LocalEndpoint = listen.Endpoint;
                server.ExternalEndpoint = this.connectionManagerSettings.ExternalEndpoint;

                this.Servers.Add(server);
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(true, this, this.loggerFactory)
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

            IPeerConnector peerConnector = this.DiscoverNodesPeerConnector;
            if ((peerConnector != null) && !peerConnector.Requirements.RequiredServices.HasFlag(services))
            {
                peerConnector.Requirements.RequiredServices |= NetworkPeerServices.NODE_WITNESS;
                foreach (NetworkPeer node in peerConnector.ConnectedPeers)
                {
                    if (!node.PeerVersion.Services.HasFlag(services))
                        node.DisconnectAsync("The peer does not support the required services requirement.");
                }
            }

            this.logger.LogTrace("(-)");
        }

        public string GetStats()
        {
            StringBuilder builder = new StringBuilder();
            lock (this.downloads)
            {
                PerformanceSnapshot diffTotal = new PerformanceSnapshot(0, 0);
                builder.AppendLine("=======Connections=======");
                foreach (NetworkPeer node in this.ConnectedNodes)
                {
                    PerformanceSnapshot newSnapshot = node.Counter.Snapshot();
                    PerformanceSnapshot lastSnapshot = null;
                    if (this.downloads.TryGetValue(node, out lastSnapshot))
                    {
                        BlockPullerBehavior behavior = node.Behaviors.OfType<BlockPullerBehavior>()
                            .FirstOrDefault(b => b.Puller.GetType() == typeof(LookaheadBlockPuller));

                        PerformanceSnapshot diff = newSnapshot - lastSnapshot;
                        diffTotal = new PerformanceSnapshot(diff.TotalReadBytes + diffTotal.TotalReadBytes, diff.TotalWrittenBytes + diffTotal.TotalWrittenBytes) { Start = diff.Start, Taken = diff.Taken };
                        builder.Append((node.RemoteSocketAddress + ":" + node.RemoteSocketPort).PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + this.ToKBSec(diff.ReadenBytesPerSecond) + "\tW:" + this.ToKBSec(diff.WrittenBytesPerSecond));
                        if (behavior != null)
                        {
                            int intQuality = (int)behavior.QualityScore;
                            builder.Append("\tQualityScore: " + intQuality + (intQuality < 10 ? "\t" : "") + "\tPendingBlocks: " + behavior.PendingDownloadsCount);
                        }

                        builder.AppendLine();
                    }

                    this.downloads.AddOrReplace(node, newSnapshot);
                }

                builder.AppendLine("=================");
                builder.AppendLine("Total".PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + this.ToKBSec(diffTotal.ReadenBytesPerSecond) + "\tW:" + this.ToKBSec(diffTotal.WrittenBytesPerSecond));
                builder.AppendLine("==========================");

                //TODO: Hack, we should just clean nodes that are not connect anymore.
                if (this.downloads.Count > 1000)
                    this.downloads.Clear();
            }

            return builder.ToString();
        }

        public string GetNodeStats()
        {
            var builder = new StringBuilder();

            foreach (NetworkPeer node in this.ConnectedNodes)
            {
                ConnectionManagerBehavior connectionManagerBehavior = node.Behavior<ConnectionManagerBehavior>();
                ChainHeadersBehavior chainHeadersBehavior = node.Behavior<ChainHeadersBehavior>();
                builder.AppendLine(
                    "Node:" + (node.RemoteInfo() + ", ").PadRight(LoggingConfiguration.ColumnLength + 15) +
                    (" connected" + " (" + (connectionManagerBehavior.Inbound ? "inbound" : "outbound") + "),").PadRight(LoggingConfiguration.ColumnLength + 7) +
                    (" agent " + node.PeerVersion.UserAgent + ", ").PadRight(LoggingConfiguration.ColumnLength + 2) +
                    " height=" + chainHeadersBehavior.PendingTip.Height);
            }

            return builder.ToString();
        }

        private string ToKBSec(ulong bytesPerSec)
        {
            double speed = ((double)bytesPerSec / 1024.0);
            return speed.ToString("0.00") + " KB/S";
        }

        private IPeerConnector CreatePeerConnector(
            NetworkPeerConnectionParameters parameters,
            NetworkPeerServices requiredServices,
            Func<IPEndPoint, byte[]> peerSelector,
            PeerIntroductionType peerIntroductionType,
            int? maximumNodeConnections = 8)
        {
            this.logger.LogTrace("({0}:{1})", nameof(requiredServices), requiredServices);

            var nodeRequirement = new NetworkPeerRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = requiredServices,
            };

            var peerConnector = new PeerConnector(this.Network, this.nodeLifetime, parameters, nodeRequirement, peerSelector, this.asyncLoopFactory, this.peerAddressManager, peerIntroductionType, this.networkPeerFactory)
            {
                MaximumNodeConnections = maximumNodeConnections.Value
            };

            this.logger.LogTrace("(-)");

            return peerConnector;
        }

        private string GetVersion()
        {
            Match match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
            return match.Groups[1].Value;
        }

        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.logger.LogInformation("Stopping peer discovery...");
            this.peerDiscoveryLoop?.Dispose();

            this.DiscoverNodesPeerConnector?.Dispose();
            this.ConnectNodePeerConnector?.Dispose();
            this.AddNodePeerConnector?.Dispose();

            foreach (NetworkPeerServer server in this.Servers)
                server.Dispose();

            foreach (NetworkPeer node in this.connectedNodes.Where(n => n.Behaviors.Find<ConnectionManagerBehavior>().OneTry))
                node.Disconnect();

            this.logger.LogTrace("(-)");
        }

        internal void AddConnectedNode(NetworkPeer node)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(node), node.RemoteSocketEndpoint);

            this.connectedNodes.Add(node);

            this.logger.LogTrace("(-)");
        }

        internal void RemoveConnectedNode(NetworkPeer node)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(node), node.RemoteSocketEndpoint);

            this.connectedNodes.Remove(node);

            this.logger.LogTrace("(-)");
        }

        public NetworkPeer FindNodeByEndpoint(IPEndPoint endpoint)
        {
            return this.connectedNodes.FindByEndpoint(endpoint);
        }

        public NetworkPeer FindNodeByIp(IPAddress ip)
        {
            return this.connectedNodes.FindByIp(ip);
        }

        public NetworkPeer FindLocalNode()
        {
            return this.connectedNodes.FindLocal();
        }

        public void AddNodeAddress(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            this.peerAddressManager.AddPeer(new NetworkAddress(endpoint), IPAddress.Loopback, PeerIntroductionType.Add);

            this.AddNodePeerConnector.MaximumNodeConnections++;

            this.logger.LogTrace("(-)");
        }

        public void RemoveNodeAddress(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            NetworkPeer node = this.connectedNodes.FindByEndpoint(endpoint);
            node?.DisconnectAsync("Requested by user");

            this.logger.LogTrace("(-)");
        }

        public NetworkPeer Connect(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
            cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory)
            {
                OneTry = true
            });

            NetworkPeer node = this.networkPeerFactory.CreateConnectedNetworkPeer(this.Network, endpoint, cloneParameters);
            this.peerAddressManager.PeerAttempted(endpoint, this.dateTimeProvider.GetUtcNow());
            node.VersionHandshake();

            this.logger.LogTrace("(-)");
            return node;
        }
    }
}
