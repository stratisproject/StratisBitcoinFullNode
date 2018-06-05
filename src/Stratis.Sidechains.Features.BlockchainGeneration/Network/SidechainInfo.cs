using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{

    public class SidechainInfo : SidechainInfoBase<NetworkInfo>
    {
        [JsonIgnore]
        public Dictionary<string, NetworkInfo> NetworkInfoByName { get; }
        public SidechainInfo(string chainName, string coinName, int coinType,
            NetworkInfoRequest mainNet, NetworkInfoRequest testNet, NetworkInfoRequest regTest)
            : base(chainName, coinName, coinType,
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.SidechainMainName, mainNet),
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.SidechainTestName, testNet),
                NetworkInfo.FromNetworkInfoRequest(SidechainNetwork.SidechainRegTestName, regTest))
        {
            NetworkInfoByName = new Dictionary<string, NetworkInfo>()
            {
                { SidechainNetwork.SidechainMainName, MainNet },
                { SidechainNetwork.SidechainTestName, TestNet },
                { SidechainNetwork.SidechainRegTestName, RegTest }
            };
        }
    }
}