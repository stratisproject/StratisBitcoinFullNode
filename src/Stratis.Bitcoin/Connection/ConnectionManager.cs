using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public interface IConnectionManager : IDisposable
    {
        NodesGroup AddNodeNodeGroup { get; }
        IReadOnlyNodesCollection ConnectedNodes { get; }
        NodesGroup ConnectNodeGroup { get; }
        NodesGroup DiscoveredNodeGroup { get; set; }
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

        public NodesGroup ConnectNodeGroup { get; private set; }
        public NodesGroup AddNodeNodeGroup { get; private set; }
        public NodesGroup DiscoveredNodeGroup { get; set; }

        public ConnectionManager(Network network, NodeConnectionParameters parameters, NodeSettings nodeSettings, ILoggerFactory loggerFactory, INodeLifetime nodeLifetime)
        {
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.connectionManagerSettings = nodeSettings.ConnectionManager;
            this.parameters = parameters;
            this.parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.Servers = new List<NodeServer>();
        }

        public void Start()
        {
            this.logger.LogTrace("()");

            this.parameters.UserAgent = $"{this.NodeSettings.Agent}:{this.GetVersion()}";
            this.parameters.Version = this.NodeSettings.ProtocolVersion;

            if (this.connectionManagerSettings.Connect.Count == 0)
            {
                NodeConnectionParameters cloneParameters = this.parameters.Clone();
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory));
                this.DiscoveredNodeGroup = CreateNodeGroup(cloneParameters, this.discoveredNodeRequiredService);
                this.DiscoveredNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByNetwork; // It is the default, but I want to use it.
            }
            else
            {
                NodeConnectionParameters cloneParameters = this.parameters.Clone();
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory));
                cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();

                var addrman = new AddressManager();
                addrman.Add(this.connectionManagerSettings.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);

                var addrmanBehavior = new AddressManagerBehavior(addrman) { PeersToDiscover = 10 };
                addrmanBehavior.Mode = AddressManagerBehaviorMode.None;
                cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

                this.ConnectNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
                this.ConnectNodeGroup.MaximumNodeConnection = this.connectionManagerSettings.Connect.Count;
                this.ConnectNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
            }

            {
                NodeConnectionParameters cloneParameters = this.parameters.Clone();
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory));
                cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
                var addrman = new AddressManager();
                addrman.Add(this.connectionManagerSettings.AddNode.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
                var addrmanBehavior = new AddressManagerBehavior(addrman) { PeersToDiscover = 10 };
                addrmanBehavior.Mode = AddressManagerBehaviorMode.AdvertizeDiscover;
                cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

                this.AddNodeNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
                this.AddNodeNodeGroup.MaximumNodeConnection = this.connectionManagerSettings.AddNode.Count;
                this.AddNodeNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
            }

            // Related the groups to each other to prevent duplicate connections.
            RelatedNodesGroups relGroups = new RelatedNodesGroups();
            relGroups.Register("Discovered", this.DiscoveredNodeGroup);
            relGroups.Register("Connect", this.ConnectNodeGroup);
            relGroups.Register("AddNode", this.AddNodeNodeGroup);
            this.DiscoveredNodeGroup?.Connect();
            this.ConnectNodeGroup?.Connect();
            this.AddNodeNodeGroup?.Connect();

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
            NodesGroup group = this.DiscoveredNodeGroup;
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

        private NodesGroup CreateNodeGroup(NodeConnectionParameters cloneParameters, NodeServices requiredServices)
        {
            this.logger.LogTrace("({0}:{1})", nameof(requiredServices), requiredServices);

            var res = new NodesGroup(this.Network, cloneParameters, new NodeRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = requiredServices,
            });

            this.logger.LogTrace("(-)");
            return res;
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

            AddressManager addrman = AddressManagerBehavior.GetAddrman(this.AddNodeNodeGroup.NodeConnectionParameters);
            addrman.Add(new NetworkAddress(endpoint));
            this.AddNodeNodeGroup.MaximumNodeConnection++;

            this.logger.LogTrace("(-)");
        }

        public void RemoveNodeAddress(IPEndPoint endpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(endpoint), endpoint);

            Node node = this.connectedNodes.FindByEndpoint(endpoint);
            if (node != null)
                node.DisconnectAsync("Requested by user");

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
            node.VersionHandshake();

            this.logger.LogTrace("(-)");
            return node;
        }
    }
}
