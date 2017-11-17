using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Maintain connection to a given set of nodes.
    /// </summary>
    internal sealed class NodeGroupBehavior : NodeBehavior
    {
        internal NodeGroupBehavior(PeerConnector parent)
        {
            this.Parent = parent;
        }

        internal PeerConnector Parent { get; private set; }

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
            {
                if (this.Parent.RemoveNode(node))
                    this.Parent.ConnectToPeersAsync();
            }
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new NodeGroupBehavior(this.Parent);
        }

        #endregion
    }
}