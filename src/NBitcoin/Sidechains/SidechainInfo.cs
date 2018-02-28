using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace NBitcoin
{
    public class SidechainInfoRequest
    {
        public string Name { get; }

        public NetworkInfoRequest MainNet { get; }
        public NetworkInfoRequest TestNet { get; }
        public NetworkInfoRequest RegTest { get; }

        [JsonConstructor]
        public SidechainInfoRequest(string name, NetworkInfoRequest mainNet, NetworkInfoRequest testNet, NetworkInfoRequest regTest)
        {
            this.Name = name;
            this.MainNet = mainNet;
            this.TestNet = testNet;
            this.RegTest = regTest;
        }
    }

    public class SidechainInfo
    {
        public string Name { get; }

        public NetworkInfo MainNet { get; }
        public NetworkInfo TestNet { get; }
        public NetworkInfo RegTest { get; }

        [JsonConstructor]
        public SidechainInfo(string name, NetworkInfo mainNet, NetworkInfo testNet, NetworkInfo regTest)
        {
            this.Name = name;
            this.MainNet = mainNet;
            this.TestNet = testNet;
            this.RegTest = regTest;
        }

        public SidechainInfo(string name, NetworkInfoRequest mainNet, NetworkInfoRequest testNet, NetworkInfoRequest regTest)
        {
            this.Name = name;
            this.MainNet = NetworkInfo.FromNetworkInfoRequest("SidechainMain", mainNet);
            this.TestNet = NetworkInfo.FromNetworkInfoRequest("SidechainTestNet", testNet);
            this.RegTest = NetworkInfo.FromNetworkInfoRequest("SidechainRegTest", regTest);
        }
    }
}