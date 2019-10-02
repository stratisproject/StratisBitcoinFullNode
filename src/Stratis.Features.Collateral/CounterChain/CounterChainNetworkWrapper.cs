using NBitcoin;

namespace Stratis.Features.Collateral.CounterChain
{
    /// <summary>
    /// Allows us to inject information about the counter chain.
    /// </summary>
    public class CounterChainNetworkWrapper
    {
        /// <summary>
        /// The "other" network that we are connecting to from this node.
        /// E.g. if this is a Cirrus sidechain gateway node, the counter-chain would be Stratis and vice versa.
        /// </summary>
        public Network CounterChainNetwork { get; }

        public CounterChainNetworkWrapper(Network counterChainNetwork)
        {
            this.CounterChainNetwork = counterChainNetwork;
        }
    }
}
