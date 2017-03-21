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
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Configuration.Settings;

namespace Stratis.Bitcoin.Connection
{
	public class ConnectionManager : IDisposable
	{
		// The maximum number of entries in an 'inv' protocol message 
		public const int MAX_INV_SZ = 50000;

		public NodesGroup DiscoveredNodeGroup
		{
			get; set;
		}


		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}

		public NodesGroup ConnectNodeGroup
		{
			get;
			private set;
		}
		public NodesGroup AddNodeNodeGroup
		{
			get;
			private set;
		}

		public NodeConnectionParameters Parameters
		{
			get { return this._Parameters; }
		}

		NodeConnectionParameters _Parameters;
		ConnectionManagerSettings _ConnectionManagerSettings;
		public ConnectionManager(Network network, NodeConnectionParameters parameters, NodeSettings nodeSettings)
		{
			_Network = network;
			_ConnectionManagerSettings = nodeSettings.ConnectionManager;
			_Parameters = parameters;
		}

		public void Start()
		{
			_Parameters.UserAgent = "StratisBitcoin:" + GetVersion();
			if (_ConnectionManagerSettings.Connect.Count == 0)
			{
				var cloneParameters = _Parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				DiscoveredNodeGroup = CreateNodeGroup(cloneParameters, _DiscoveredNodeRequiredService);
				DiscoveredNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByNetwork; //is the default, but I want to use it
				DiscoveredNodeGroup.Connect();
			}
			else
			{
				var cloneParameters = _Parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
				var addrman = new AddressManager();
				addrman.Add(_ConnectionManagerSettings.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.None;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				ConnectNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
				ConnectNodeGroup.MaximumNodeConnection = _ConnectionManagerSettings.Connect.Count;
				ConnectNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				ConnectNodeGroup.Connect();
			}

			{
				var cloneParameters = _Parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
				var addrman = new AddressManager();
				addrman.Add(_ConnectionManagerSettings.AddNode.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.AdvertizeDiscover;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				AddNodeNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
				AddNodeNodeGroup.MaximumNodeConnection = _ConnectionManagerSettings.AddNode.Count;
				AddNodeNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				AddNodeNodeGroup.Connect();
			}

			StringBuilder logs = new StringBuilder();
			logs.AppendLine("Node listening on:");
			foreach (var listen in _ConnectionManagerSettings.Listen)
			{
				var cloneParameters = _Parameters.Clone();
				var server = new NodeServer(Network);
				server.LocalEndpoint = listen.Endpoint;
				server.ExternalEndpoint = _ConnectionManagerSettings.ExternalEndpoint;
				_Servers.Add(server);
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(true, this)
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
			Logs.ConnectionManager.LogInformation(logs.ToString());
		}

		NodeServices _DiscoveredNodeRequiredService = NodeServices.Network;
		public void AddDiscoveredNodesRequirement(NodeServices services)
		{
			_DiscoveredNodeRequiredService |= services;
			var group = DiscoveredNodeGroup;
			if (group != null &&
			   !group.Requirements.RequiredServices.HasFlag(services))
			{
				group.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
				foreach (var node in group.ConnectedNodes)
				{
					if (!node.PeerVersion.Services.HasFlag(services))
						node.DisconnectAsync("The peer does not support the required services requirement");
				}
			}
		}

		public string GetStats()
		{
			StringBuilder builder = new StringBuilder();
			lock (_Downloads)
			{
				PerformanceSnapshot diffTotal = new PerformanceSnapshot(0, 0);
				builder.AppendLine("=======Connections=======");
				foreach (var node in ConnectedNodes)
				{
					var newSnapshot = node.Counter.Snapshot();
					PerformanceSnapshot lastSnapshot = null;
					if (_Downloads.TryGetValue(node, out lastSnapshot))
					{
						var behavior = node.Behaviors.OfType<BlockPuller.BlockPullerBehavior>()
								.FirstOrDefault(b => b.Puller.GetType() == typeof(LookaheadBlockPuller));
						var diff = newSnapshot - lastSnapshot;
						diffTotal = new PerformanceSnapshot(diff.TotalReadenBytes + diffTotal.TotalReadenBytes, diff.TotalWrittenBytes + diffTotal.TotalWrittenBytes) { Start = diff.Start, Taken = diff.Taken };
						builder.Append((node.RemoteSocketAddress + ":" + node.RemoteSocketPort).PadRight(Logs.ColumnLength * 2) + "R:" + ToKBSec(diff.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diff.WrittenBytesPerSecond));
						if (behavior != null)
						{
							builder.Append("\tQualityScore: " + behavior.QualityScore + (behavior.QualityScore < 10 ? "\t" : "") + "\tPendingBlocks: " + behavior.PendingDownloads.Count);
						}
						builder.AppendLine();
					}
					_Downloads.AddOrReplace(node, newSnapshot);
				}
				builder.AppendLine("=================");
				builder.AppendLine("Total".PadRight(Logs.ColumnLength * 2) + "R:" + ToKBSec(diffTotal.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diffTotal.WrittenBytesPerSecond));
				builder.AppendLine("==========================");

				//TODO: Hack, we should just clean nodes that are not connect anymore
				if (_Downloads.Count > 1000)
					_Downloads.Clear();
			}
			return builder.ToString();
		}

		public string GetNodeStats()
		{
			var builder = new StringBuilder();

			foreach (var node in this.ConnectedNodes)
			{
				var connectionManagerBehavior = node.Behavior<ConnectionManagerBehavior>();
				var chainBehavior = node.Behavior<BlockStore.ChainBehavior>();
				builder.AppendLine(
					"Node:" + (node.RemoteInfo() + ", ").PadRight(Logs.ColumnLength + 15) +
					(" connected" + " (" + (connectionManagerBehavior.Inbound ? "inbound" : "outbound") + "),").PadRight(Logs.ColumnLength + 7) +
					(" agent " + node.PeerVersion.UserAgent + ", ").PadRight(Logs.ColumnLength + 2) +
					" height=" + chainBehavior.PendingTip.Height);
			}
			return builder.ToString();
		}

		private string ToKBSec(ulong bytesPerSec)
		{
			double speed = ((double)bytesPerSec / 1024.0);
			return speed.ToString("0.00") + " KB/S";
		}

		Dictionary<Node, PerformanceSnapshot> _Downloads = new Dictionary<Node, PerformanceSnapshot>();

		List<NodeServer> _Servers = new List<NodeServer>();

		public List<NodeServer> Servers => this._Servers;

		private NodesGroup CreateNodeGroup(NodeConnectionParameters cloneParameters, NodeServices requiredServices)
		{
			return new NodesGroup(Network, cloneParameters, new NodeRequirement()
			{
				MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
				RequiredServices = requiredServices,
			});
		}

		private string GetVersion()
		{
			var match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
			return match.Groups[1].Value;
		}

		public void Dispose()
		{
			if (DiscoveredNodeGroup != null)
				DiscoveredNodeGroup.Dispose();
			if (ConnectNodeGroup != null)
				ConnectNodeGroup.Dispose();
			if (AddNodeNodeGroup != null)
				AddNodeNodeGroup.Dispose();
			foreach (var server in _Servers)
				server.Dispose();
			foreach (var node in ConnectedNodes.Where(n => n.Behaviors.Find<ConnectionManagerBehavior>().OneTry))
				node.Disconnect();
		}


		private readonly NodesCollection _ConnectedNodes = new NodesCollection();
		public NodesCollection ConnectedNodes
		{
			get
			{
				return _ConnectedNodes;
			}
		}

		public void AddNode(IPEndPoint endpoint)
		{
			var addrman = AddressManagerBehavior.GetAddrman(AddNodeNodeGroup.NodeConnectionParameters);
			addrman.Add(new NetworkAddress(endpoint));
			AddNodeNodeGroup.MaximumNodeConnection++;
		}

		public void RemoveNode(IPEndPoint endpoint)
		{
			var node = ConnectedNodes.FindByEndpoint(endpoint);
			node.DisconnectAsync("Requested by user");
		}

		public Node Connect(IPEndPoint endpoint)
		{
			var cloneParameters = _Parameters.Clone();
			cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this)
			{
				OneTry = true
			});
			var node = Node.Connect(Network, endpoint, cloneParameters);
			node.VersionHandshake();
			return node;
		}
	}
}
