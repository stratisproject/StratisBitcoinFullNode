using NBitcoin;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>
    /// Holds information about the network we are connecting to.
    /// </summary>
    public class CounterChainNetworkHolder
    {
        public Network Network { get; }

        public CounterChainNetworkHolder(Network network)
        {
            this.Network = network;
        }
    }
}
