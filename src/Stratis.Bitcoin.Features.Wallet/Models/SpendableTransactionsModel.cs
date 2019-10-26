using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class SpendableTransactionsModel
    {
        public SpendableTransactionsModel()
        {
            this.SpendableTransactions = new List<SpendableTransactionModel>();
        }

        [JsonProperty(PropertyName = "transactions")]
        public List<SpendableTransactionModel> SpendableTransactions { get; set; }
    }

    public class SpendableTransactionModel
    {
        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        /// <summary>
        /// The index of the output in the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A value indicating whether this address is a change address.
        /// </summary>
        [JsonProperty(PropertyName = "isChange")]
        public bool IsChange { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The number of confirmations.
        /// </summary>
        [JsonProperty(PropertyName = "confirmations")]
        public int Confirmations { get; set; }
    }
}
