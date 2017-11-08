namespace NBitcoin.Protocol.Behaviors
{
    /// <summary>
    /// Maintain connection to a given set of nodes.
    /// </summary>
    internal class NodesGroupBehavior : NodeBehavior
    {
        internal NodesGroup parent;

        public NodesGroupBehavior()
        {
        }

        public NodesGroupBehavior(NodesGroup parent)
        {
            this.parent = parent;
        }

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
            {
                this.parent._ConnectedNodes.Add(node);
            }

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Disconnecting) || (node.State == NodeState.Offline))
            {
                if (this.parent._ConnectedNodes.Remove(node))
                {
                    this.parent.StartConnecting();
                }
            }
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new NodesGroupBehavior(this.parent);
        }

        #endregion
    }
}