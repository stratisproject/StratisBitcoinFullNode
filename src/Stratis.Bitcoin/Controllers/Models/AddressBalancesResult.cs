using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Controllers.Models
{
    public sealed class AddressIndexerBalancesResult
    {
        public AddressIndexerBalancesResult()
        {
            this.Balances = new List<AddressIndexerBalanceResult>();
        }

        public List<AddressIndexerBalanceResult> Balances { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; private set; }

        public static AddressIndexerBalancesResult IndexerNotQueryable(string reason)
        {
            return new AddressIndexerBalancesResult() { Reason = reason };
        }
    }

    public sealed class AddressIndexerBalanceResult
    {
        public AddressIndexerBalanceResult() { }

        public AddressIndexerBalanceResult(string address, Money balance)
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