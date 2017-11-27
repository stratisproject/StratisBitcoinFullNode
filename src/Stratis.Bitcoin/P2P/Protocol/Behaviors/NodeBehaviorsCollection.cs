using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    public class NodeBehaviorsCollection : ThreadSafeCollection<INodeBehavior>
    {
        private readonly NetworkPeer node;
        private bool delayAttach;

        public NodeBehaviorsCollection(NetworkPeer node)
        {
            this.node = node;
        }

        bool CanAttach
        {
            get
            {
                return (this.node != null) && !this.DelayAttach && (this.node.State != NetworkPeerState.Offline) && (this.node.State != NetworkPeerState.Failed) && (this.node.State != NetworkPeerState.Disconnecting);
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