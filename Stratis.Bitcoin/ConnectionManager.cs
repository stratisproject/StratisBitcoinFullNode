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
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketAddress + " connected with agent " + node.PeerVersion.UserAgent);
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
		public NodesGroup OutboundNodeGroup
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
		public ConnectionManager(Network network, NodeConnectionParameters parameters)
		{
			_Network = network;
			parameters.UserAgent = "StratisBitcoin:" + GetVersion();
			var outboundParameters = parameters.Clone();
			parameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this));
			OutboundNodeGroup = new NodesGroup(network, parameters, new NodeRequirement()
			{
				MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
				RequiredServices = NodeServices.Network
			});
			OutboundNodeGroup.Connect();
		}

		private string GetVersion()
		{
			var match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
			return match.Groups[1].Value;
		}

		public void Dispose()
		{
			OutboundNodeGroup.Dispose();
		}
	}
}
