using NBitcoin;
using System;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol.Filters
{
    public class NodeFiltersCollection : ThreadSafeCollection<INodeFilter>
    {
        public IDisposable Add(Action<IncomingMessage, Action> onReceiving, Action<Node, Payload, Action> onSending = null)
        {
            return base.Add(new ActionFilter(onReceiving, onSending));
        }
    }
}
