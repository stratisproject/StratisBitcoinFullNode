using System.Net;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Base peer event.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public abstract class PeerEventBase : EventBase
    {
        /// <summary>
        /// Gets the peer end point.
        /// </summary>
        /// <value>
        /// The peer end point.
        /// </value>
        public IPEndPoint PeerEndPoint { get; }

        public PeerEventBase(IPEndPoint peerEndPoint)
        {
            this.PeerEndPoint = peerEndPoint;
        }
    }
}