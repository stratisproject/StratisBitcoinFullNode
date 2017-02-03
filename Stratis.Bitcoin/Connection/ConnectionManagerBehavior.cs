using System;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.Connection
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
}