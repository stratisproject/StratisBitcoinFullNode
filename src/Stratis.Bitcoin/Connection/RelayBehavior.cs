using System.Threading.Tasks;
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
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        private Task OnMessageReceivedAsync(NetworkPeer peer, IncomingMessage message)
        {
            return Task.CompletedTask;
        }

        private void AttachedNode_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged -= this.AttachedNode_StateChanged;
        }
    }
}
