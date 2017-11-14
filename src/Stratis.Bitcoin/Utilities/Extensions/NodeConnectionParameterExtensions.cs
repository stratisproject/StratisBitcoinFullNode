using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P;

namespace Stratis.Bitcoin
{
    public static class NodeConnectionParameterExtensions
    {
        public static PeerAddressManagerBehaviour PeerAddressManagerBehaviour(this NodeConnectionParameters parameters)
        {
            return parameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
        }

        public static PeerAddressManager PeerAddressManager(this NodeConnectionParameters parameters)
        {
            var behaviour = parameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
            return behaviour.AddressManager;
        }
    }
}