using System.Net;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever a peer connection attempt failed.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerConnectionAttemptFailed : EventBase
    {
        public bool Inbound { get; }

        public IPEndPoint RemoteEndPoint { get; }

        public string Reason { get; }

        public PeerConnectionAttemptFailed(bool inbound, IPEndPoint remoteEndPoint, string reason)
        {
            this.Inbound = inbound;
            this.RemoteEndPoint = remoteEndPoint;
            this.Reason = reason;
        }
    }
}