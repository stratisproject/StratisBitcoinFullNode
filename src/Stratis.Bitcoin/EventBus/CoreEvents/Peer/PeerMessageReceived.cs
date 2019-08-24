using System.Net;
using Stratis.Bitcoin.P2P.Protocol;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// A peer message has been received and parsed
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerMessageReceived : PeerEventBase
    {
        public Message Message { get; }

        public int MessageSize { get; }

        public PeerMessageReceived(IPEndPoint peerEndPoint, Message message, int messageSize) : base(peerEndPoint)
        {
            this.Message = message;
            this.MessageSize = messageSize;
        }
    }
}