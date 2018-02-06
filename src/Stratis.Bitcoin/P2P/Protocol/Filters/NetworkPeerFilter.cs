using System;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol.Filters
{
    /// <summary>
    /// Contract to intercept sent and received messages.
    /// </summary>
    public interface INetworkPeerFilter
    {
        /// <summary>
        /// Intercept a message before it can be processed by listeners
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="next">The rest of the pipeline</param>
        void OnReceivingMessage(IncomingMessage message, Action next);

        /// <summary>
        /// Intercept a message before it is sent to the peer
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="payload"></param>
        /// <param name="next">The rest of the pipeline</param>
        void OnSendingMessage(INetworkPeer peer, Payload payload, Action next);
    }
}
