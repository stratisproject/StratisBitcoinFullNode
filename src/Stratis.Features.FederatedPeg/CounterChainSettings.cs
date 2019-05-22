using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg
{
    public class CounterChainSettings : ICounterChainSettings
    {
        public const string CounterChainApiHostParam = "counterchainapihost";

        public const string CounterChainApiPortParam = "counterchainapiport";

        public string CounterChainApiHost { get; set; }

        public int CounterChainApiPort { get; set; }

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
