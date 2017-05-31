using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Wallet.Models
{
    public class WalletBalanceModel
    {
        [JsonProperty(PropertyName = "balances")]
        public List<AccountBalance> AccountsBalances { get; set; }
    }

    public class AccountBalance
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
    }
}
