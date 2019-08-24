using System.Collections.Generic;
using LiteDB;
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

    /// <summary>
    /// A class that contains a list of balances data per address as requested.
    /// <para>Should the request fail the <see cref="Reason"/> will be populated.</para>
    /// </summary>
    public sealed class VerboseAddressBalancesResult
    {
        private VerboseAddressBalancesResult()
        {
            this.BalancesData = new List<AddressIndexerData>();
        }

        public VerboseAddressBalancesResult(int consensusTipHeight) : this()
        {
            this.ConsensusTipHeight = consensusTipHeight;
        }

        public List<AddressIndexerData> BalancesData { get; set; }

        public int ConsensusTipHeight { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; private set; }

        public static VerboseAddressBalancesResult RequestFailed(string reason)
        {
            return new VerboseAddressBalancesResult() { Reason = reason };
        }
    }

    public class AddressIndexerData
    {
        [BsonId]
        public string Address { get; set; }

        public List<AddressBalanceChange> BalanceChanges { get; set; }
    }

    public class AddressBalanceChange
    {
        /// <summary><c>true</c> if there was a deposit to an address, <c>false</c> if it was a withdrawal.</summary>
        public bool Deposited { get; set; }

        public long Satoshi { get; set; }

        /// <summary>Height of a block in which operation was confirmed.</summary>
        public int BalanceChangedHeight { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.Deposited)}:{this.Deposited}, {nameof(this.Satoshi)}:{this.Satoshi}, {nameof(this.BalanceChangedHeight)}:{this.BalanceChangedHeight}";
        }
    }

    public static class AddressBalanceChangeExtensions
    {
        public static long CalculateBalance(this IEnumerable<AddressBalanceChange> balanceChanges)
        {
            long balance = 0;

            foreach (AddressBalanceChange change in balanceChanges)
            {
                if (change.Deposited)
                    balance += change.Satoshi;
                else
                    balance -= change.Satoshi;
            }

            return balance;
        }
    }
}