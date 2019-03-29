using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Features.FederatedPeg
{
    public class FederatedPegOptions
    {
        public IServiceCollection Services { get; }
        public NodeSettings NodeSettings { get; }

        public FederatedPegOptions(IServiceCollection services, NodeSettings nodeSettings)
        {
            this.Services = services;
            this.NodeSettings = nodeSettings;
        }

        public void SetCounterChainNetwork(Network network)
        {
            this.Services.AddSingleton(new CounterChainNetworkHolder(network));
        }

        public void SetCounterChainNetworkSelector(NetworksSelector networksSelector)
        {
            // Get correct network using copied code from NodeSettings
            Network network = this.GetCounterChainNetwork(networksSelector);

            // Inject him
            this.SetCounterChainNetwork(network);
        }

        private Network GetCounterChainNetwork(NetworksSelector networksSelector)
        {
            // Find out if we need to run on testnet or regtest from the config file.
            bool testNet = this.NodeSettings.ConfigReader.GetOrDefault<bool>("testnet", false);
            bool regTest = this.NodeSettings.ConfigReader.GetOrDefault<bool>("regtest", false);

            if (testNet && regTest)
                throw new ConfigurationException("Invalid combination of regtest and testnet.");

            return testNet ? networksSelector.Testnet() : regTest ? networksSelector.Regtest() : networksSelector.Mainnet();
        }
    }
}
