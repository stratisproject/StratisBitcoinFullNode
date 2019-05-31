using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.PoA.Models
{
    public class PoAInfoModel
    {
        [JsonProperty(PropertyName = "miningPubKey", NullValueHandling = NullValueHandling.Ignore)]
        public string MiningPublicKey { get; set; }

        [JsonProperty(PropertyName = "federationMiningPubKeys", NullValueHandling = NullValueHandling.Ignore)]
        public IEnumerable<string> FederationMiningPubKeys { get; set; }
    }
}
