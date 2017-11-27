﻿using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin
{
    public static class NodeConnectionParameterExtensions
    {
        public static PeerAddressManagerBehaviour PeerAddressManagerBehaviour(this NodeConnectionParameters parameters)
        {
            return parameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
        }
    }
}
