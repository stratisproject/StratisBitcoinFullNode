using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    // Bitcoin Core 0.17.0
    //    {
    //  "version": xxxxx,                      (numeric) the server version
    //  "subversion": "/Satoshi:x.x.x/",     (string) the server subversion string
    //  "protocolversion": xxxxx,              (numeric) the protocol version
    //  "localservices": "xxxxxxxxxxxxxxxx", (string) the services we offer to the network
    //  "localrelay": true|false,              (bool) true if transaction relay is requested from peers
    //  "timeoffset": xxxxx,                   (numeric) the time offset
    //  "connections": xxxxx,                  (numeric) the number of connections
    //  "networkactive": true|false,           (bool) whether p2p networking is enabled
    //  "networks": [                          (array) information per network
    //  {
    //    "name": "xxx",                     (string) network(ipv4, ipv6 or onion)
    //    "limited": true|false,               (boolean) is the network limited using -onlynet?
    //    "reachable": true|false,             (boolean) is the network reachable?
    //    "proxy": "host:port"               (string) the proxy that is used for this network, or empty if none
    //    "proxy_randomize_credentials": true|false, (string) Whether randomized credentials are used
    //  }
    //  ,...
    //  ],
    // * "relayfee": x.xxxxxxxx,                (numeric) minimum relay fee for transactions in BTC/kB
    // * "incrementalfee": x.xxxxxxxx,          (numeric) minimum fee increment for mempool limiting or BIP 125 replacement in BTC/kB
    //  "localaddresses": [                    (array) list of local addresses
    //  {
    //    "address": "xxxx",                 (string) network address
    //    "port": xxx,                         (numeric) network port
    //    "score": xxx(numeric) relative score
    //  }
    //  ,...
    //  ]
    //  "warnings": "..."                    (string) any network and blockchain warnings
    //}
    public class NetworkInfoModel
    {
        [JsonProperty(PropertyName = "version")]
        public uint Version { get; set; }

        [JsonProperty(PropertyName = "subversion")]
        public string SubVersion { get; set; }

        [JsonProperty(PropertyName = "protocolversion")]
        public uint ProtocolVersion { get; set; }

        [JsonProperty(PropertyName = "localservices")]
        public string LocalServices { get; set; }

        [JsonProperty(PropertyName = "localrelay")]
        public bool IsLocalRelay { get; set; }

        [JsonProperty(PropertyName = "timeoffset")]
        public long TimeOffset { get; set; }

        [JsonProperty(PropertyName = "connections", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Connections { get; set; }

        [JsonProperty(PropertyName = "networkactive")]
        public bool IsNetworkActive { get; set; }

        [JsonProperty(PropertyName = "relayfee")]
        public decimal RelayFee { get; set; }

        [JsonProperty(PropertyName = "incrementalfee")]
        public decimal IncrementalFee { get; set; }
    }
}
