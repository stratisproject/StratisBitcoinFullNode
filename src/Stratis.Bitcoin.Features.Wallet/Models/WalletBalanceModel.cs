using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletBalanceModel
    {
        public WalletBalanceModel()
        {
            this.AccountsBalances = new List<AccountBalanceModel>();
        }

        [JsonProperty(PropertyName = "balances")]
        public List<AccountBalanceModel> AccountsBalances { get; set; }
    }

    public class AccountBalanceModel
    {
        [JsonProperty(PropertyName = "accountName")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "accountHdPath")]
        public string HdPath { get; set; }

        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        [JsonProperty(PropertyName = "amountConfirmed")]
        public Money AmountConfirmed { get; set; }

        [JsonProperty(PropertyName = "amountUnconfirmed")]
        public Money AmountUnconfirmed { get; set; }

        [JsonProperty(PropertyName = "spendableAmount")]
        public Money SpendableAmount { get; set; }
    }
}
