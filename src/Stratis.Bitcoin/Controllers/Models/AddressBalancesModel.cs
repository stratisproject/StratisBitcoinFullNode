using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Controllers.Models
{
    public sealed class AddressBalancesModel
    {
        public AddressBalancesModel()
        {
            this.Balances = new List<AddressBalanceModel>();
        }

        public List<AddressBalanceModel> Balances { get; set; }
    }

    public sealed class AddressBalanceModel
    {
        public AddressBalanceModel() { }

        public AddressBalanceModel(string address, Money balance)
        {
            this.Address = address;
            this.Balance = balance;
        }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("balance")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Balance { get; set; }
    }
}
