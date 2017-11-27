using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    public class RelayBehavior : NodeBehavior
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
            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
        }

        private void AttachedNode_MessageReceived(NetworkPeer node, IncomingMessage message)
        {
        }

        private void AttachedNode_StateChanged(NetworkPeer node, NetworkPeerState oldState)
        {
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
        }
    }
}
