using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    ///  A class that contains a list of balances per address as requested.
    ///  <para>
    ///  Should the request fail the <see cref="Reason"/> will be populated.
    ///  </para>
    /// </summary>
    public sealed class AddressBalancesResult
    {
        public AddressBalancesResult()
        {
            this.Balances = new List<AddressBalanceResult>();
        }

        public List<AddressBalanceResult> Balances { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; private set; }

        public static AddressBalancesResult RequestFailed(string reason)
        {
            return new AddressBalancesResult() { Reason = reason };
        }
    }

    /// <summary>
    ///  A class that contains the balance for a given address.
    /// </summary>
    public sealed class AddressBalanceResult
    {
        public AddressBalanceResult() { }

        public AddressBalanceResult(string address, Money balance)
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