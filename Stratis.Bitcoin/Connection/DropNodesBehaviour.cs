using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// If the light wallet is only connected to nodes behind 
    /// it cannot progress progress to the tip to get the full balance
    /// this behaviour will make sure place is kept for nodes higher then 
    /// current tip.
    /// </summary>
    public class DropNodesBehaviour : NodeBehavior
    {
        private readonly ConcurrentChain chain;
        private readonly IConnectionManager connection;
        private readonly decimal dropTrashold;

        public DropNodesBehaviour(ConcurrentChain chain, IConnectionManager connectionManager)
        {
            this.chain = chain;
            this.connection = connectionManager;

            // 80% of current max connections, the last 20% will only 
            // connect to nodes ahead of the current best chain
            this.dropTrashold = 0.8M; 
        }

        private void AttachedNodeOnMessageReceived(Node node, IncomingMessage message)
        {
            message.Message.IfPayloadIs<VersionPayload>(version =>
            {
                var nodeGroup = this.connection.DiscoveredNodeGroup ?? this.connection.ConnectNodeGroup;
                // find how much 20% max nodes 
                var connectAbove = Math.Round(nodeGroup.MaximumNodeConnection * this.dropTrashold, MidpointRounding.ToEven);

                if (connectAbove < this.connection.ConnectedNodes.Count())
                    if (version.StartHeight < this.chain.Height)
                        this.AttachedNode.DisconnectAsync($"Node too far behind height = {version.StartHeight}");
            });
        }

        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += this.AttachedNodeOnMessageReceived;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= this.AttachedNodeOnMessageReceived;
        }

        public override object Clone()
        {
            return new DropNodesBehaviour(this.chain, this.connection);
        }
    }
}
