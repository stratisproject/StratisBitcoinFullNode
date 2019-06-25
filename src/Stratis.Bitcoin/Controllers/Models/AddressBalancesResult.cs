using System.Collections.Generic;
using LiteDB;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A class that contains a list of balances data per address as requested.
    /// <para>Should the request fail the <see cref="Reason"/> will be populated.</para>
    /// </summary>
    public sealed class VerboseAddressBalancesResult
    {
        public VerboseAddressBalancesResult()
        {
            this.BalancesData = new List<AddressIndexerData>();
        }

        public List<AddressIndexerData> BalancesData { get; set; }

        [JsonProperty("consensusTipHeight")]
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