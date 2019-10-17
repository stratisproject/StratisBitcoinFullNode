using System.Runtime.Serialization;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class ListSinceBlockTransactionModel
    {
        /// <summary>
        /// The account for the transaction
        /// </summary>
        [JsonProperty("account")]
        public string Account { get; set; } = "";

        /// <summary>
        /// The address involved in the transaction.
        /// For 'send' it's the external destination address it was sent to, for 'receive' it's the wallet address it was received into.
        /// </summary>
        [JsonProperty("address")]
        public string Address { get; set; }

        /// <summary>
        /// The transaction category.
        /// </summary>
        [JsonProperty("category")]
        public ListSinceBlockTransactionCategoryModel Category { get; set; }

        /// <summary>
        /// The total amount received or spent in this transaction.
        /// Can be positive (received), negative (sent) or 0 (payment to yourself).
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// The amount of the fee. This is negative and only available for the 'send' category of transactions.
        /// </summary>
        [JsonProperty("fee", NullValueHandling = NullValueHandling.Ignore)]
        public decimal? Fee { get; set; }

        /// <summary>
        /// The number of confirmations.
        /// </summary>
        [JsonProperty("confirmations", NullValueHandling = NullValueHandling.Ignore)]
        public int Confirmations { get; set; }

        /// <summary>
        /// The block hash.
        /// </summary>
        [JsonProperty("blockhash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// The index of the transaction in the block that includes it.
        /// </summary>
        [JsonProperty("blockindex", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockIndex { get; set; }

        /// <summary>
        /// The time in seconds since epoch (1 Jan 1970 GMT).
        /// </summary>
        [JsonProperty(PropertyName = "blocktime", NullValueHandling = NullValueHandling.Ignore)]
        public long? BlockTime { get; set; }

        /// <summary>
        /// The transaction id.
        /// </summary>
        [JsonProperty("txid")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// The transaction time in seconds since epoch (1 Jan 1970 GMT).
        /// </summary>
        [JsonProperty("time")]
        public long TransactionTime { get; set; }

        /// <summary>
        /// The time received in seconds since epoch (1 Jan 1970 GMT).
        /// </summary>
        [JsonProperty(PropertyName = "timereceived")]
        public long TimeReceived { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ListSinceBlockTransactionCategoryModel
    {
        /// <summary>
        /// Non-coinbase transactions received.
        /// </summary>
        [EnumMember(Value = "receive")]
        Receive,

        /// <summary>
        /// Transactions sent.
        /// </summary>
        [EnumMember(Value = "send")]
        Send,

        /// <summary>
        /// Mature coinbase or coinstake transactions.
        /// </summary>
        [EnumMember(Value = "generate")]
        Generate,

        /// <summary>
        /// Immature coinbase or coinstake transactions.
        /// </summary>
        [EnumMember(Value = "immature")]
        Immature,

        /// <summary>
        /// Orphaned coinbase or coinstake transactions received.
        /// </summary>
        [EnumMember(Value = "orphan")]
        Orphan
    }
}
