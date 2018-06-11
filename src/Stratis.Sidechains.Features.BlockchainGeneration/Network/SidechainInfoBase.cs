using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{
    public class SidechainInfoBase<T> where T : INetworkInfoRequest
    {
        public string ChainName { get; }
        public string CoinName { get; }
        public int CoinType { get; }
        public T MainNet { get; }
        public T TestNet { get; }
        public T RegTest { get; }

        [JsonConstructor]
        public SidechainInfoBase(string chainName, string coinName, int coinType, T mainNet, T testNet, T regTest)
        {
            this.ChainName = chainName;
            this.CoinName = coinName;
            this.CoinType = coinType;
            this.MainNet = mainNet;
            this.TestNet = testNet;
            this.RegTest = regTest;
        }
    }
}