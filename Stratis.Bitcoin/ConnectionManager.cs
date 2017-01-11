using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Stratis.Bitcoin.Configuration;
using System.Net;
using System.Text;
using Stratis.Bitcoin.BlockPulling;
using NBitcoin.Protocol.Payloads;

namespace Stratis.Bitcoin
{
	public class ConnectionManagerBehavior : NodeBehavior
	{
		public ConnectionManagerBehavior(bool inbound, ConnectionManager connectionManager)
		{
			Inbound = inbound;
			ConnectionManager = connectionManager;
		}

		public ConnectionManager ConnectionManager
		{
			get;
			private set;
		}
		public bool Inbound
		{
			get;
			private set;
		}
		public bool Whitelisted
		{
			get;
			internal set;
		}
		public bool OneTry
		{
			get;
			internal set;
		}

		public override object Clone()
		{
			return new ConnectionManagerBehavior(Inbound, ConnectionManager)
			{
				OneTry = OneTry,
				Whitelisted = Whitelisted,
			};
		}

		protected override void AttachCore()
		{
			this.AttachedNode.StateChanged += AttachedNode_StateChanged;
			this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			_ChainBehavior = this.AttachedNode.Behaviors.Find<ChainBehavior>();
		}

		ChainBehavior _ChainBehavior;
		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if(_ChainBehavior.InvalidHeaderReceived && !Whitelisted)
			{
				node.DisconnectAsync("Invalid block received");
			}
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if(node.State == NodeState.HandShaked)
			{
				ConnectionManager.ConnectedNodes.Add(node);
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketAddress + " connected (" + (Inbound ? "inbound" : "outbound") + "), agent " + node.PeerVersion.UserAgent + ", height " + node.PeerVersion.StartHeight);
				node.SendMessageAsync(new SendHeadersPayload());
			}
			if(node.State == NodeState.Failed || node.State == NodeState.Offline)
			{
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketAddress + " offline");
				if(node.DisconnectReason != null && !String.IsNullOrEmpty(node.DisconnectReason.Reason))
					Logs.ConnectionManager.LogInformation("Reason: " + node.DisconnectReason.Reason);
				ConnectionManager.ConnectedNodes.Remove(node);
			}
		}

		protected override void DetachCore()
		{
			this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
			this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
		}
	}

	public class ConnectionManager : IDisposable
	{
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

		NodeConnectionParameters _Parameters;
		ConnectionManagerArgs _Args;
		public ConnectionManager(Network network, NodeConnectionParameters parameters, ConnectionManagerArgs args)
		{
			_Network = network;
			_Args = args;
			_Parameters = parameters;
		}

		public void Start()
		{
			_Parameters.UserAgent = "StratisBitcoin:" + GetVersion();
			if(_Args.Connect.Count == 0)
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
				addrman.Add(_Args.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.None;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				ConnectNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
				ConnectNodeGroup.MaximumNodeConnection = _Args.Connect.Count;
				ConnectNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				ConnectNodeGroup.Connect();
			}

			{
				var cloneParameters = _Parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
				var addrman = new AddressManager();
				addrman.Add(_Args.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.AdvertizeDiscover;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				AddNodeNodeGroup = CreateNodeGroup(cloneParameters, NodeServices.Nothing);
				AddNodeNodeGroup.MaximumNodeConnection = _Args.AddNode.Count;
				AddNodeNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				AddNodeNodeGroup.Connect();
			}

			StringBuilder logs = new StringBuilder();
			logs.AppendLine("Node listening on:");
			foreach(var listen in _Args.Listen)
			{
				var cloneParameters = _Parameters.Clone();
				var server = new NodeServer(Network);
				server.LocalEndpoint = listen.Endpoint;
				server.ExternalEndpoint = _Args.ExternalEndpoint;
				_Servers.Add(server);
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(true, this)
				{
					Whitelisted = listen.Whitelisted
				});
				server.InboundNodeConnectionParameters = cloneParameters;
				server.Listen();
				logs.Append(listen.Endpoint.Address + ":" + listen.Endpoint.Port);
				if(listen.Whitelisted)
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
			if(group != null &&
			   !group.Requirements.RequiredServices.HasFlag(services))
			{
				group.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
				foreach(var node in group.ConnectedNodes)
				{
					if(!node.PeerVersion.Services.HasFlag(services))
						node.DisconnectAsync("The peer does not support the required services requirement");
				}
			}
		}

		public string GetStats()
		{
			StringBuilder builder = new StringBuilder();
			lock(_Downloads)
			{
				PerformanceSnapshot diffTotal = new PerformanceSnapshot(0, 0);
				builder.AppendLine("=======Connections=======");
				foreach(var node in ConnectedNodes)
				{
					var newSnapshot = node.Counter.Snapshot();
					PerformanceSnapshot lastSnapshot = null;
					if(_Downloads.TryGetValue(node, out lastSnapshot))
					{
						var behavior = node.Behaviors.Find<NodesBlockPuller.NodesBlockPullerBehavior>();
						var diff = newSnapshot - lastSnapshot;
						diffTotal = new PerformanceSnapshot(diff.TotalReadenBytes + diffTotal.TotalReadenBytes, diff.TotalWrittenBytes + diffTotal.TotalWrittenBytes) { Start = diff.Start, Taken = diff.Taken };
						builder.Append((node.RemoteSocketAddress + ":" + node.RemoteSocketPort).PadRight(Logs.ColumnLength * 2) + "R:" + ToKBSec(diff.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diff.WrittenBytesPerSecond));
						if(behavior != null)
						{
							builder.Append("\tQualityScore: " + behavior.QualityScore + "\tPendingBlocks: " + behavior.PendingDownloads.Count);
						}
						builder.AppendLine();
					}
					_Downloads.AddOrReplace(node, newSnapshot);
				}
				builder.AppendLine("=================");
				builder.AppendLine("Total".PadRight(Logs.ColumnLength * 2) + "R:" + ToKBSec(diffTotal.ReadenBytesPerSecond) + "\tW:" + ToKBSec(diffTotal.WrittenBytesPerSecond));
				builder.AppendLine("==========================");

				//TODO: Hack, we should just clean nodes that are not connect anymore
				if(_Downloads.Count > 1000)
					_Downloads.Clear();
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
			if(DiscoveredNodeGroup != null)
				DiscoveredNodeGroup.Dispose();
			if(ConnectNodeGroup != null)
				ConnectNodeGroup.Dispose();
			if(AddNodeNodeGroup != null)
				AddNodeNodeGroup.Dispose();
			foreach(var server in _Servers)
				server.Dispose();
			foreach(var node in ConnectedNodes.Where(n => n.Behaviors.Find<ConnectionManagerBehavior>().OneTry))
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
