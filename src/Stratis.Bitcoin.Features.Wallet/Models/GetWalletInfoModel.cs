using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>Model for RPC method getwalletinfo.</summary>
    public class GetWalletInfoModel
    {
        [JsonProperty("walletname")]
        public string WalletName { get; set; }

        [JsonProperty("walletversion")]
        public int WalletVersion { get; set; }

        [JsonProperty("balance")]
        [JsonConverter(typeof(MoneyInCoinsJsonConverter))]
        public Money Balance { get; set; }

        [JsonProperty("unconfirmed_balance")]
        [JsonConverter(typeof(MoneyInCoinsJsonConverter))]
        public Money UnConfirmedBalance { get; set; }

        [JsonProperty("immature_balance")]
        [JsonConverter(typeof(MoneyInCoinsJsonConverter))]
        public Money ImmatureBalance { get; set; }
    }
}
