using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Collateral.CounterChain
{
    public class CounterChainSettings : ICounterChainSettings
    {
        /// <summary>
        /// Command-line argument to set counter chain host.
        /// </summary>
        public const string CounterChainApiHostParam = "counterchainapihost";

        /// <summary>
        /// Command-line argument to set counter chain port.
        /// </summary>
        public const string CounterChainApiPortParam = "counterchainapiport";

        /// <inheritdoc />
        public string CounterChainApiHost { get; set; }

        /// <inheritdoc />
        public int CounterChainApiPort { get; set; }

        /// <inheritdoc />
        public Network CounterChainNetwork { get; set; }

        public CounterChainSettings(NodeSettings nodeSettings, Network counterChainNetwork)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration configReader = nodeSettings.ConfigReader;

            this.CounterChainApiHost = configReader.GetOrDefault(CounterChainApiHostParam, "localhost");
            this.CounterChainApiPort = configReader.GetOrDefault(CounterChainApiPortParam, counterChainNetwork.DefaultAPIPort);
            this.CounterChainNetwork = counterChainNetwork;
        }
    }
}
