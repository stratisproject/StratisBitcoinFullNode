using System.Linq;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class NodeConnectionParameterExtensions
    {
        public static PeerAddressManagerBehaviour PeerAddressManagerBehaviour(this NetworkPeerConnectionParameters parameters)
        {
            return parameters.TemplateBehaviors.OfType<PeerAddressManagerBehaviour>().FirstOrDefault();
        }
    }
}