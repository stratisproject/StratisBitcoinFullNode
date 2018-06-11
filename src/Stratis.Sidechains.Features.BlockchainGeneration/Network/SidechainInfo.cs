using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{

    public class SidechainInfo : SidechainInfoBase<NetworkInfo>
    {
        [JsonIgnore]
        public Dictionary<string, NetworkInfo> NetworkInfoByName { get; }
        public SidechainInfo(string chainName, string coinName, int coinType,
            NetworkInfoRequest mainNet, NetworkInfoRequest testNet, NetworkInfoRequest regTest)
            : base(chainName, coinName, coinType,
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.MainNetworkName, mainNet),
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.TestNetworkName, testNet),
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.RegTestNetworkName, regTest))
        {
            NetworkInfoByName = new Dictionary<string, NetworkInfo>()
            {
                { SidechainNetwork.MainNetworkName, MainNet },
                { SidechainNetwork.TestNetworkName, TestNet },
                { SidechainNetwork.RegTestNetworkName, RegTest }
            };
        }
    }
}