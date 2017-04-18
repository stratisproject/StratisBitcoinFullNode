using System;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.Connection
{
	public class ConnectionManagerBehavior : NodeBehavior
	{
		public ConnectionManagerBehavior(bool inbound, ConnectionManager connectionManager)
		{
			this.Inbound = inbound;
			this.ConnectionManager = connectionManager;
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
			return new ConnectionManagerBehavior(this.Inbound, this.ConnectionManager)
			{
				OneTry = this.OneTry,
				Whitelisted = this.Whitelisted,
			};
		}

		protected override void AttachCore()
		{
			this.AttachedNode.StateChanged += AttachedNode_StateChanged;
			this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			this._ChainBehavior = this.AttachedNode.Behaviors.Find<BlockStore.ChainBehavior>();
		}

		BlockStore.ChainBehavior _ChainBehavior;
		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if(this._ChainBehavior.InvalidHeaderReceived && !this.Whitelisted)
			{
				node.DisconnectAsync("Invalid block received");
			}
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if(node.State == NodeState.HandShaked)
			{
				this.ConnectionManager.AddConnectedNode(node);
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketEndpoint + " connected (" + (this.Inbound ? "inbound" : "outbound") + "), agent " + node.PeerVersion.UserAgent + ", height " + node.PeerVersion.StartHeight);
				node.SendMessageAsync(new SendHeadersPayload());
			}
			if(node.State == NodeState.Failed || node.State == NodeState.Offline)
			{
				Logs.ConnectionManager.LogInformation("Node " + node.RemoteSocketEndpoint + " offline");
				if(node.DisconnectReason != null && !string.IsNullOrEmpty(node.DisconnectReason.Reason))
					Logs.ConnectionManager.LogInformation("Reason: " + node.DisconnectReason.Reason);
				this.ConnectionManager.RemoveConnectedNode(node);
			}
		}

		protected override void DetachCore()
		{
			this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
			this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
		}
	}
}