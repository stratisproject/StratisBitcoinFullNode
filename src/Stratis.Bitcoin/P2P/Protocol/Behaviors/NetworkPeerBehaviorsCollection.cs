using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    public class NetworkPeerBehaviorsCollection : ThreadSafeCollection<INetworkPeerBehavior>
    {
        private readonly INetworkPeer peer;
        private bool delayAttach;

        public NetworkPeerBehaviorsCollection(INetworkPeer peer)
        {
            this.peer = peer;
        }

        private bool CanAttach
        {
            get
            {
                return (this.peer != null) && !this.DelayAttach && (this.peer.State != NetworkPeerState.Offline) && (this.peer.State != NetworkPeerState.Failed) && (this.peer.State != NetworkPeerState.Disconnecting);
            }
        }

        protected override void OnAdding(INetworkPeerBehavior obj)
        {
            if (this.CanAttach)
                obj.Attach(this.peer);
        }

        protected override void OnRemoved(INetworkPeerBehavior obj)
        {
            if (obj.AttachedPeer != null)
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
                    foreach (INetworkPeerBehavior behavior in this)
                        behavior.Attach(this.peer);
                }
            }
        }
    }
}