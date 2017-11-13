namespace NBitcoin.Protocol.Behaviors
{
    public class NodeBehaviorsCollection : ThreadSafeCollection<INodeBehavior>
    {
        private readonly Node node;
        private bool delayAttach;

        public NodeBehaviorsCollection(Node node)
        {
            this.node = node;
        }

        bool CanAttach
        {
            get
            {
                return (this.node != null) && !this.DelayAttach && (this.node.State != NodeState.Offline) && (this.node.State != NodeState.Failed) && (this.node.State != NodeState.Disconnecting);
            }
        }

        protected override void OnAdding(INodeBehavior obj)
        {
            if (this.CanAttach)
                obj.Attach(this.node);
        }

        protected override void OnRemoved(INodeBehavior obj)
        {
            if (obj.AttachedNode != null)
                obj.Detach();
        }

        internal bool DelayAttach
        {
            get
            {
                return this.delayAttach;
            }
            set
            {
                this.delayAttach = value;
                if (this.CanAttach)
                {
                    foreach (INodeBehavior behavior in this)
                        behavior.Attach(this.node);
                }
            }
        }
    }
}