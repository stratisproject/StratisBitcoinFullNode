using System;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol.Filters
{
    public class ActionFilter : INetworkPeerFilter
    {
        private readonly Action<IncomingMessage, Action> onIncoming;
        private readonly Action<INetworkPeer, Payload, Action> onSending;

        public ActionFilter(Action<IncomingMessage, Action> onIncoming = null, Action<INetworkPeer, Payload, Action> onSending = null)
        {
            this.onIncoming = onIncoming ?? new Action<IncomingMessage, Action>((m, n) => n());
            this.onSending = onSending ?? new Action<INetworkPeer, Payload, Action>((m, p, n) => n());
        }

        public void OnReceivingMessage(IncomingMessage message, Action next)
        {
            this.onIncoming(message, next);
        }

        public void OnSendingMessage(INetworkPeer peer, Payload payload, Action next)
        {
            this.onSending(peer, payload, next);
        }
    }
}
