﻿using System.Net;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.EventBus.CoreEvents
{
    /// <summary>
    /// Event that is published whenever a peer connects to the node.
    /// This happens prior to any Payload they have to exchange.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class PeerConnected : EventBase
    {
        public bool Inbound { get; }

        public IPEndPoint PeerEndPoint { get; }

        public PeerConnected(bool inbound, IPEndPoint peerEndPoint)
        {
            this.Inbound = inbound;
            this.PeerEndPoint = peerEndPoint;
        }
    }
}