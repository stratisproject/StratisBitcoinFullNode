using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    public class RelayBehavior : NetworkPeerBehavior
    {
        public RelayBehavior()
        {
        }

        public override object Clone()
        {
            return new RelayBehavior();
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.StateChanged += this.AttachedNode_StateChanged;
            this.AttachedPeer.MessageReceived += this.AttachedNode_MessageReceived;
        }

        private void AttachedNode_MessageReceived(NetworkPeer node, IncomingMessage message)
        {
        }

        private void AttachedNode_StateChanged(NetworkPeer node, NetworkPeerState oldState)
        {
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.StateChanged -= this.AttachedNode_StateChanged;
            this.AttachedPeer.MessageReceived -= this.AttachedNode_MessageReceived;
        }
    }
}
