using System;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.P2P.Protocol.Filters
{
    public class ActionFilter : INodeFilter
    {
        private readonly Action<IncomingMessage, Action> onIncoming;
        private readonly Action<NetworkPeer, Payload, Action> onSending;
        public ActionFilter(Action<IncomingMessage, Action> onIncoming = null, Action<NetworkPeer, Payload, Action> onSending = null)
        {
            this.onIncoming = onIncoming ?? new Action<IncomingMessage, Action>((m, n) => n());
            this.onSending = onSending ?? new Action<NetworkPeer, Payload, Action>((m, p, n) => n());
        }

        public void OnReceivingMessage(IncomingMessage message, Action next)
        {
            this.onIncoming(message, next);
        }

        public void OnSendingMessage(NetworkPeer node, Payload payload, Action next)
        {
            this.onSending(node, payload, next);
        }
    }
}
