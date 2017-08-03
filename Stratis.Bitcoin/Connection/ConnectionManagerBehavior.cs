using System;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Connection
{
	public class ConnectionManagerBehavior : NodeBehavior
	{
	    private readonly ILogger logger;

	    public ConnectionManagerBehavior(bool inbound, IConnectionManager connectionManager, ILogger logger)
		{
		    this.logger = logger;
		    this.Inbound = inbound;
			this.ConnectionManager = connectionManager as ConnectionManager;
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
			return new ConnectionManagerBehavior(this.Inbound, this.ConnectionManager, this.logger)
			{
				OneTry = this.OneTry,
				Whitelisted = this.Whitelisted,
			};
		}

		protected override void AttachCore()
		{
			this.AttachedNode.StateChanged += AttachedNode_StateChanged;
			this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			this.chainHeadersBehavior = this.AttachedNode.Behaviors.Find<ChainHeadersBehavior>();
		}

		ChainHeadersBehavior chainHeadersBehavior;
		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if(this.chainHeadersBehavior.InvalidHeaderReceived && !this.Whitelisted)
			{
				node.DisconnectAsync("Invalid block received");
			}
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if(node.State == NodeState.HandShaked)
			{
				this.ConnectionManager.AddConnectedNode(node);
				this.logger.LogInformation("Node " + node.RemoteSocketEndpoint + " connected (" + (this.Inbound ? "inbound" : "outbound") + "), agent " + node.PeerVersion.UserAgent + ", height " + node.PeerVersion.StartHeight);
				node.SendMessageAsync(new SendHeadersPayload());
			}
			if(node.State == NodeState.Failed || node.State == NodeState.Offline)
			{
			    this.logger.LogInformation("Node " + node.RemoteSocketEndpoint + " offline");
				if(node.DisconnectReason != null && !string.IsNullOrEmpty(node.DisconnectReason.Reason))
				    this.logger.LogInformation("Reason: " + node.DisconnectReason.Reason);
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