using Newtonsoft.Json;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletFileModel
    {
        [JsonProperty(PropertyName = "walletsPath")]
        public string WalletsPath { get; set; }

        [JsonProperty(PropertyName = "walletsFiles")]
        public IEnumerable<string> WalletsFiles { get; set; }
    }
}
