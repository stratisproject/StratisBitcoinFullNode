using NBitcoin.Protocol;
using System;

namespace NBitcoin.Protocol.Filters
{
    public class ActionFilter : INodeFilter
    {
        private readonly Action<IncomingMessage, Action> onIncoming;
        private readonly Action<Node, Payload, Action> onSending;
        public ActionFilter(Action<IncomingMessage, Action> onIncoming = null, Action<Node, Payload, Action> onSending = null)
        {
            this.onIncoming = onIncoming ?? new Action<IncomingMessage, Action>((m, n) => n());
            this.onSending = onSending ?? new Action<Node, Payload, Action>((m, p, n) => n());
        }

        public void OnReceivingMessage(IncomingMessage message, Action next)
        {
            this.onIncoming(message, next);
        }

        public void OnSendingMessage(Node node, Payload payload, Action next)
        {
            this.onSending(node, payload, next);
        }
    }
}
