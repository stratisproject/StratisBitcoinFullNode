using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Maintain connection to a given set of peers.</summary>
    internal sealed class PeerConnectorBehaviour : NodeBehavior
    {
        /// <summary>The peer connector this behaviour relates to.</summary>
        private readonly IPeerConnector peerConnector;

        internal PeerConnectorBehaviour(IPeerConnector peerConnector)
        {
            this.peerConnector = peerConnector;
        }

        /// <inheritdoc/>
        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
        }

        /// <inheritdoc/>
        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
        }

        /// <inheritdoc/>
        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
                this.peerConnector.AddNode(node);

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Disconnecting) || (node.State == NodeState.Offline))
                this.peerConnector.RemoveNode(node);
        }

        #region ICloneable Members

        /// <inheritdoc/>
        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.peerConnector);
        }

        #endregion
    }
}