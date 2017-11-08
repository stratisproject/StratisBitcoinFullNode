namespace NBitcoin.Protocol.Behaviors
{
    /// <summary>
    /// Maintain connection to a given set of nodes.
    /// </summary>
    internal class NodesGroupBehavior : NodeBehavior
    {
        internal NodesGroup Parent;

        public NodesGroupBehavior()
        {
        }

        public NodesGroupBehavior(NodesGroup parent)
        {
            this.Parent = parent;
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
                this.Parent._ConnectedNodes.Add(node);
            }

            if ((node.State == NodeState.Failed) || (node.State == NodeState.Disconnecting) || (node.State == NodeState.Offline))
            {
                if (this.Parent._ConnectedNodes.Remove(node))
                {
                    this.Parent.StartConnecting();
                }
            }
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new NodesGroupBehavior(this.Parent);
        }

        #endregion
    }
}