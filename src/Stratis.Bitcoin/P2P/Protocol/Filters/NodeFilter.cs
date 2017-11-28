using System;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol.Filters
{
    /// <summary>
    /// A NodeFilter can intercept messages received and sent.
    /// </summary>
    public interface INodeFilter
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
        /// <param name="node"></param>
        /// <param name="payload"></param>
        /// <param name="next">The rest of the pipeline</param>
        void OnSendingMessage(NetworkPeer node, Payload payload, Action next);
    }
}
