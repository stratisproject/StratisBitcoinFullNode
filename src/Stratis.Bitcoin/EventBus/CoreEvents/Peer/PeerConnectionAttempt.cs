using System.Net;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever the node tries to connect to a peer.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerConnectionAttempt : PeerEventBase
    {
        public bool Inbound { get; }

        public PeerConnectionAttempt(bool inbound, IPEndPoint peerEndPoint) : base(peerEndPoint)
        {
            this.Inbound = inbound;
        }
    }
}