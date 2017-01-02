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


		public override object Clone()
		{
			return new ConnectionManagerBehavior(Inbound, ConnectionManager);
		}

		protected override void AttachCore()
		{
			this.AttachedNode.StateChanged += AttachedNode_StateChanged;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if(node.State == NodeState.HandShaked)
			{
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketAddress + " connected (" + (Inbound ? "inbound" : "outbound" )+ "), agent " + node.PeerVersion.UserAgent + ", height " + node.PeerVersion.StartHeight);
			}
			if(node.State == NodeState.Failed || node.State == NodeState.Offline)
			{
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketAddress + " offline");
				if(node.DisconnectReason != null)
					Logs.ConnectionManager.LogInformation("Reason: " + node.DisconnectReason.Reason);
			}
		}

		protected override void DetachCore()
		{
			this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
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

		public ConnectionManager(Network network, NodeConnectionParameters parameters, ConnectionManagerArgs args)
		{
			_Network = network;
			parameters.UserAgent = "StratisBitcoin:" + GetVersion();



			if(args.Connect.Count == 0)
			{
				var cloneParameters = parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				DiscoveredNodeGroup = CreateNodeGroup(cloneParameters);
				DiscoveredNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByNetwork; //is the default, but I want to use it
				DiscoveredNodeGroup.Connect();
			}
			else
			{
				var cloneParameters = parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
				var addrman = new AddressManager();
				addrman.Add(args.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.None;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				ConnectNodeGroup = CreateNodeGroup(cloneParameters);
				ConnectNodeGroup.MaximumNodeConnection = args.Connect.Count;
				ConnectNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				ConnectNodeGroup.Connect();
			}

			{
				var cloneParameters = parameters.Clone();
				cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
				cloneParameters.TemplateBehaviors.Remove<AddressManagerBehavior>();
				var addrman = new AddressManager();
				addrman.Add(args.Connect.Select(c => new NetworkAddress(c)).ToArray(), IPAddress.Loopback);
				var addrmanBehavior = new AddressManagerBehavior(addrman);
				addrmanBehavior.Mode = AddressManagerBehaviorMode.AdvertizeDiscover;
				cloneParameters.TemplateBehaviors.Add(addrmanBehavior);

				AddNodeNodeGroup = CreateNodeGroup(cloneParameters);
				AddNodeNodeGroup.MaximumNodeConnection = args.AddNode.Count;
				AddNodeNodeGroup.CustomGroupSelector = WellKnownGroupSelectors.ByEndpoint;
				AddNodeNodeGroup.Connect();
			}
		}

		private NodesGroup CreateNodeGroup(NodeConnectionParameters cloneParameters)
		{
			return new NodesGroup(Network, cloneParameters, new NodeRequirement()
			{
				MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
				RequiredServices = NodeServices.Network,
			});
		}

		private string GetVersion()
		{
			var match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
			return match.Groups[1].Value;
		}

		public void Dispose()
		{
			DiscoveredNodeGroup.Dispose();
			if(ConnectNodeGroup != null)
				ConnectNodeGroup.Dispose();
			AddNodeNodeGroup.Dispose();
		}
	}
}
