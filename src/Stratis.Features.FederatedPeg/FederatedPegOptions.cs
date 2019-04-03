using NBitcoin;

namespace Stratis.Features.FederatedPeg
{
    public class FederatedPegOptions
    {
        public Network CounterChainNetwork { get; }

        public FederatedPegOptions(Network counterChainNetwork)
        {
            this.CounterChainNetwork = counterChainNetwork;
        }
    }
}
