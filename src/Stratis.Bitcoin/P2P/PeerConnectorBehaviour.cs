using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Maintain connection to a given set of peers.
    /// </summary>
    internal sealed class PeerConnectorBehaviour : NodeBehavior
    {
        internal PeerConnectorBehaviour(PeerConnector parent)
        {
            this.Parent = parent;
        }

        internal readonly PeerConnector Parent;

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
                this.Parent.AddNode(node);

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Disconnecting) || (node.State == NodeState.Offline))
                this.Parent.RemoveNode(node);
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.Parent);
        }

        #endregion
    }
}