using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Maintain connection to a given set of peers.</summary>
    internal sealed class PeerConnectorBehaviour : NodeBehavior
    {
        /// <summary>The peer connector this behaviour relates to.</summary>
        private readonly PeerConnector peerConnector;

        internal PeerConnectorBehaviour(PeerConnector peerConnector)
        {
            this.peerConnector = peerConnector;
        }

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
        }

        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
                this.peerConnector.AddNode(node);

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Disconnecting) || (node.State == NodeState.Offline))
                this.peerConnector.RemoveNode(node);
        }

        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.peerConnector);
        }
    }
}
