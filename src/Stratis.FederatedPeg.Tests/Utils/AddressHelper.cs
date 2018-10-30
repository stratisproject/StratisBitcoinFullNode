using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.Tests.Utils
{
    public class AddressHelper
    {
        private readonly Network network;

        public readonly Key Key;
        public readonly BitcoinSecret SourceChainSecret;
        public readonly BitcoinSecret TargetChainSecret;
        public readonly BitcoinPubKeyAddress SourceChainAddress;
        public readonly BitcoinPubKeyAddress TargetChainAddress;

        public AddressHelper(Network network)
        {
            this.network = network;

            this.Key = new Key();
            this.SourceChainSecret = this.network.CreateBitcoinSecret(this.Key);
            this.SourceChainAddress = this.SourceChainSecret.GetAddress();

            this.TargetChainSecret = this.network.ToCounterChainNetwork().CreateBitcoinSecret(this.Key);
            this.TargetChainAddress = this.TargetChainSecret.GetAddress();
        }

        public BitcoinPubKeyAddress GetNewSourceChainAddress()
        {
            var key = new Key();
            var newAddress = this.network.CreateBitcoinSecret(key).GetAddress();
            return newAddress;
        }

        public BitcoinPubKeyAddress GetNewTargetChainAddress()
        {
            var key = new Key();
            var newAddress = this.network.ToCounterChainNetwork().CreateBitcoinSecret(key).GetAddress();
            return newAddress;
        }
    }
}
