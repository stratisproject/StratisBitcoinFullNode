using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.FederatedSidechains.Initialisation
{
    public class SidechainInitialisationConfigModel
    {
        public string ChainName { get; set; }
        public string CoinSymbol { get; set; }
        public uint CoinType { get; set; }
        public string CoinbaseText { get; set; }
        public string BaseChain { get; set; }
        public NetworkInitialisationConfigModel MainNet { get; set; }
        public NetworkInitialisationConfigModel TestNet { get; set; }
        public NetworkInitialisationConfigModel RegTest { get; set; }
    }

    public class NetworkInitialisationConfigModel
    {
        public uint AddressPrefix { get; set; }
        public uint Port { get; set; }
        public uint RpcPort { get; set; }
        public uint ApiPort { get; set; }
        public string PowLimit { get; set; }
        public uint Magic { get; set; }
        public uint MinTxFee { get; set; }
        public uint FallbackFee { get; set; }
        public uint MinRelayTxFee { get; set; }
    }
}
