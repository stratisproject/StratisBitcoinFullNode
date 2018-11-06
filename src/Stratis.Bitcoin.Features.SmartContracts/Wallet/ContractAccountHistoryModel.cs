using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public class ContractAccountHistoryModel
    {
        public ContractAccountHistoryModel()
        {
            this.TransactionsHistory = new List<ContractTransactionItemModel>();
        }

        [JsonProperty(PropertyName = "accountName")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "accountHdPath")]
        public string HdPath { get; set; }

        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        [JsonProperty(PropertyName = "transactionsHistory")]
        public ICollection<ContractTransactionItemModel> TransactionsHistory { get; set; }
    }
}
