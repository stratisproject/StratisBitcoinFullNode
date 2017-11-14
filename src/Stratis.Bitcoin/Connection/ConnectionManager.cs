using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public interface IConnectionManager : IDisposable
    {
        NodeGroup AddNodeNodeGroup { get; }
        IReadOnlyNodesCollection ConnectedNodes { get; }
        NodeGroup ConnectNodeGroup { get; }
        NodeGroup DiscoveredNodeGroup { get; set; }
        Network Network { get; }
        NodeSettings NodeSettings { get; }
        NodeConnectionParameters Parameters { get; }
        List<NodeServer> Servers { get; }

        void AddDiscoveredNodesRequirement(NodeServices services);
        void AddNodeAddress(IPEndPoint endpoint);
        Node Connect(IPEndPoint endpoint);
        Node FindLocalNode();
        Node FindNodeByEndpoint(IPEndPoint endpoint);
        Node FindNodeByIp(IPAddress ip);
        string GetNodeStats();
        string GetStats();
        void RemoveNodeAddress(IPEndPoint endpoint);
        void Start();
    }

    public class ConnectionManager : IConnectionManager
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        // The maximum number of entries in an 'inv' protocol message.
        public const int MaxInventorySize = 50000;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly INodeLifetime nodeLifetime;

        private readonly NodesCollection connectedNodes = new NodesCollection();
        public IReadOnlyNodesCollection ConnectedNodes { get { return this.connectedNodes; } }

        private readonly Dictionary<Node, PerformanceSnapshot> downloads = new Dictionary<Node, PerformanceSnapshot>();
        private NodeServices discoveredNodeRequiredService = NodeServices.Network;
        private readonly ConnectionManagerSettings connectionManagerSettings;

        private readonly Network network;
        public Network Network { get { return this.network; } }

        private readonly NodeConnectionParameters parameters;
        public NodeConnectionParameters Parameters { get { return this.parameters; } }

        private readonly NodeSettings nodeSettings;
        public NodeSettings NodeSettings { get { return this.nodeSettings; } }

        public List<NodeServer> Servers { get; }

        public NodeGroup ConnectNodeGroup { get; private set; }
        public NodeGroup AddNodeNodeGroup { get; private set; }
        public NodeGroup DiscoveredNodeGroup { get; set; }

        public ConnectionManager(Network network, NodeConnectionParameters parameters, NodeSettings nodeSettings, ILoggerFactory loggerFactory, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
        {
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.connectionManagerSettings = nodeSettings.ConnectionManager;
            this.parameters = parameters;
            this.parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.asyncLoopFactory = asyncLoopFactory;

            this.Servers = new List<NodeServer>();
        }

        public void Start()
        {
            this.logger.LogTrace("()");

            this.parameters.UserAgent = $"{this.NodeSettings.Agent}:{this.GetVersion()}";
            this.parameters.Version = this.NodeSettings.ProtocolVersion;

            NodeConnectionParameters clonedParameters = this.parameters.Clone();
            clonedParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory));

            if (!this.connectionManagerSettings.Connect.Any())
            {
                this.DiscoveredNodeGroup = CreateNodeGroup(clonedParameters, this.discoveredNodeRequiredService, WellKnownGroupSelectors.ByNetwork);
            }
            else
            {
                clonedParameters.PeerAddressManager().AddPeer(this.connectionManagerSettings.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
                clonedParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;

                this.ConnectNodeGroup = CreateNodeGroup(clonedParameters, NodeServices.Nothing, WellKnownGroupSelectors.ByEndpoint);
                this.ConnectNodeGroup.MaximumNodeConnections = this.connectionManagerSettings.Connect.Count;
            }

            {
                clonedParameters.PeerAddressManager().AddPeer(this.connectionManagerSettings.AddNode.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
                clonedParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;

                this.AddNodeNodeGroup = CreateNodeGroup(clonedParameters, NodeServices.Nothing, WellKnownGroupSelectors.ByEndpoint);
                this.AddNodeNodeGroup.MaximumNodeConnections = this.connectionManagerSettings.AddNode.Count;
            }

            // Relate the groups to each other to prevent duplicate connections.
            var relGroups = new RelatedNodeGroups();
            relGroups.Register("Discovered", this.DiscoveredNodeGroup);
            relGroups.Register("Connect", this.ConnectNodeGroup);
            relGroups.Register("AddNode", this.AddNodeNodeGroup);

            this.DiscoveredNodeGroup?.ConnectToPeersAsync();
            this.ConnectNodeGroup?.ConnectToPeersAsync();
            this.AddNodeNodeGroup?.ConnectToPeersAsync();

            StringBuilder logs = new StringBuilder();
            logs.AppendLine("Node listening on:");
            foreach (NodeServerEndpoint listen in this.connectionManagerSettings.Listen)
            {
                NodeConnectionParameters cloneParameters = this.parameters.Clone();
                var server = new NodeServer(this.Network)
                {
                    LocalEndpoint = listen.Endpoint,
                    ExternalEndpoint = this.connectionManagerSettings.ExternalEndpoint
                };

                this.Servers.Add(server);
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(true, this, this.loggerFactory)
                {
                    Whitelisted = listen.Whitelisted
                });

                server.InboundNodeConnectionParameters = cloneParameters;
                server.Listen();

                logs.Append(listen.Endpoint.Address + ":" + listen.Endpoint.Port);
                if (listen.Whitelisted)
                    logs.Append(" (whitelisted)");
                logs.AppendLine();
            }
            this.logger.LogInformation(logs.ToString());

            this.logger.LogTrace("(-)");
        }

        public void AddDiscoveredNodesRequirement(NodeServices services)
        {
            this.logger.LogTrace("({0}:{1})", nameof(services), services);

            this.discoveredNodeRequiredService |= services;
            NodeGroup group = this.DiscoveredNodeGroup;
            if ((group != null) && !group.Requirements.RequiredServices.HasFlag(services))
            {
                group.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
                foreach (Node node in group.ConnectedNodes)
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
                foreach (Node node in this.ConnectedNodes)
                {
                    PerformanceSnapshot newSnapshot = node.Counter.Snapshot();
                    PerformanceSnapshot lastSnapshot = null;
                    if (this.downloads.TryGetValue(node, out lastSnapshot))
                    {
                        BlockPullerBehavior behavior = node.Behaviors.OfType<BlockPullerBehavior>()
                            .FirstOrDefault(b => b.Puller.GetType() == typeof(LookaheadBlockPuller));

                        PerformanceSnapshot diff = newSnapshot - lastSnapshot;
                        diffTotal = new PerformanceSnapshot(diff.TotalReadBytes + diffTotal.TotalReadBytes, diff.TotalWrittenBytes + diffTotal.TotalWrittenBytes) { Start = diff.Start, Taken = diff.Taken };
                        builder.Append((node.RemoteSocketAddress + ":" + node.RemoteSocketPort).PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + ToKBSec(diff.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diff.WrittenBytesPerSecond));
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
                builder.AppendLine("Total".PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + ToKBSec(diffTotal.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diffTotal.WrittenBytesPerSecond));
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

            foreach (Node node in this.ConnectedNodes)
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

        private NodeGroup CreateNodeGroup(NodeConnectionParameters parameters, NodeServices requiredServices, Func<IPEndPoint, byte[]> groupSelector)
        {
            this.logger.LogTrace("({0}:{1})", nameof(requiredServices), requiredServices);

            var nodeRequirement = new NodeRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = requiredServices,
            };

            var nodeGroup = new NodeGroup(this.Network, this.nodeLifetime, parameters, nodeRequirement, groupSelector, this.asyncLoopFactory);

            this.logger.LogTrace("(-)");

            return nodeGroup;
        }

        private string GetVersion()
        {
            Match match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
            return match.Groups[1].Value;
        }

        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.DiscoveredNodeGroup?.Dispose();
            this.ConnectNodeGroup?.Dispose();
            this.AddNodeNodeGroup?.Dispose();

            foreach (NodeServer server in this.Servers)
                server.Dispose();

            foreach (Node node in this.connectedNodes.Where(n => n.Behaviors.Find<ConnectionManagerBehavior>().OneTry))
                node.Disconnect();

            this.logger.LogTrace("(-)");
        }

        internal void AddConnectedNode(Node node)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(node), node.RemoteSocketEndpoint);

            this.connectedNodes.Add(node);

            this.logger.LogTrace("(-)");
        }

        internal void RemoveConnectedNode(Node node)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(node), node.RemoteSocketEndpoint);

            this.connectedNodes.Remove(node);

            this.logger.LogTrace("(-)");
        }

        public Node FindNodeByEndpoint(IPEndPoint endpoint)
        {
            return this.connectedNodes.FindByEndpoint(endpoint);
        }

        public Node FindNodeByIp(IPAddress ip)
        {
            return this.connectedNodes.FindByIp(ip);
        }

        public Node FindLocalNode()
        {
            return this.connectedNodes.FindLocal();
        }

        public void AddNodeAddress(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            PeerAddressManager addressManager = this.AddNodeNodeGroup.Parameters.PeerAddressManager();
            addressManager.AddPeer(PeerAddress.Create(new NetworkAddress(endpoint)));

            this.AddNodeNodeGroup.MaximumNodeConnections++;

            this.logger.LogTrace("(-)");
        }

        public void RemoveNodeAddress(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            Node node = this.connectedNodes.FindByEndpoint(endpoint);
            node?.DisconnectAsync("Requested by user");

            this.logger.LogTrace("(-)");
        }

        public Node Connect(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            NodeConnectionParameters cloneParameters = this.parameters.Clone();
            cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory)
            {
                OneTry = true
            });

            Node node = Node.Connect(this.Network, endpoint, cloneParameters);
            cloneParameters.PeerAddressManager().PeerAttempted(endpoint, DateTimeOffset.Now);
            node.VersionHandshake();

            this.logger.LogTrace("(-)");
            return node;
        }
    }
}