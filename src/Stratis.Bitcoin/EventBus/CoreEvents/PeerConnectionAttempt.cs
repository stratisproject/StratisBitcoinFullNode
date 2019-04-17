using System.Net;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever the node tries to connect to a peer.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerConnectionAttempt : EventBase
    {
        public bool Inbound { get; }

        public IPEndPoint PeerEndPoint { get; }

        public PeerConnectionAttempt(bool inbound, IPEndPoint peerEndPoint)
        {
            this.Inbound = inbound;
            this.PeerEndPoint = peerEndPoint;
        }
    }
}