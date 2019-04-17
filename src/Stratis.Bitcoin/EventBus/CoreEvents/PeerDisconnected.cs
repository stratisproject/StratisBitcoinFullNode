using System;
using System.Net;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever a peer disconnects from the node.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerDisconnected : EventBase
    {
        public bool Inbound { get; }

        public IPEndPoint RemoteEndPoint { get; }

        public string Reason { get; }

        public Exception Exception { get; }

        public PeerDisconnected(bool inbound, IPEndPoint remoteEndPoint, string reason, Exception exception)
        {
            this.Inbound = inbound;
            this.RemoteEndPoint = remoteEndPoint;
            this.Reason = reason;
            this.Exception = exception;
        }
    }
}