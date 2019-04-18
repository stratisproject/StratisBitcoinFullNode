using System;
using System.Net;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever a peer disconnects from the node.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerDisconnected : PeerEventBase
    {
        public bool Inbound { get; }

        public string Reason { get; }

        public Exception Exception { get; }

        public PeerDisconnected(bool inbound, IPEndPoint remoteEndPoint, string reason, Exception exception) : base(remoteEndPoint)
        {
            this.Inbound = inbound;
            this.Reason = reason;
            this.Exception = exception;
        }
    }
}