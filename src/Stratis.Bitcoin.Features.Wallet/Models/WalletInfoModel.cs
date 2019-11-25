using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletInfoModel
    {
        public WalletInfoModel()
        {
        }

        public WalletInfoModel(IEnumerable<string> walletNames)
        {
            this.WalletNames = walletNames;
        }

        [JsonProperty(PropertyName = "walletNames")]
        public IEnumerable<string> WalletNames { get; set; }
    }
}