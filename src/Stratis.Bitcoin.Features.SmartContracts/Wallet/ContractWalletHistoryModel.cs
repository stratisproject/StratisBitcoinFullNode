using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public class ContractWalletHistoryModel
    {
        public ContractWalletHistoryModel()
        {
            this.AccountsHistoryModel = new List<ContractAccountHistoryModel>();
        }

        [JsonProperty(PropertyName = "history")]
        public ICollection<ContractAccountHistoryModel> AccountsHistoryModel { get; set; }
    }
}
