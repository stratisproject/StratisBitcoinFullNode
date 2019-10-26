using Newtonsoft.Json;
using Stratis.Bitcoin.Controllers.Converters;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class GetInfoModel
    {
        [JsonProperty(Order = 0, PropertyName = "version")]
        public uint Version { get; set; }

        [JsonProperty(Order = 1, PropertyName = "protocolversion")]
        public uint ProtocolVersion { get; set; }

        [JsonProperty(Order = 4, PropertyName = "blocks")]
        public int Blocks { get; set; }

        [JsonProperty(Order = 5, PropertyName = "timeoffset")]
        public long TimeOffset { get; set; }

        [JsonProperty(Order = 6, PropertyName = "connections", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Connections { get; set; }

        [JsonProperty(Order = 7, PropertyName = "proxy")]
        public string Proxy { get; set; }

        [JsonProperty(Order = 8, PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty(Order = 9, PropertyName = "testnet")]
        public bool Testnet { get; set; }

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 14, PropertyName = "relayfee")]
        public decimal RelayFee { get; set; }

        [JsonProperty(Order = 15, PropertyName = "errors")]
        public string Errors { get; set; }

        [JsonProperty(Order = 2, PropertyName = "walletversion", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? WalletVersion { get; set; }

        [JsonProperty(Order = 3, PropertyName = "balance", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal? Balance { get; set; }

        [JsonProperty(Order = 10, PropertyName = "keypoololdest", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long? KeypoolOldest { get; set; }

        [JsonProperty(Order = 11, PropertyName = "keypoolsize", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? KeypoolSize { get; set; }

        [JsonProperty(Order = 12, PropertyName = "unlocked_until", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? UnlockedUntil { get; set; }

        [JsonProperty(Order = 13, PropertyName = "paytxfee", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public decimal? PayTxFee { get; set; }
    }
}
