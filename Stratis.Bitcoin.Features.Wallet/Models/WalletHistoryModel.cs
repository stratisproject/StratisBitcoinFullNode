using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletHistoryModel
    {
        public WalletHistoryModel()
        {
            this.TransactionsHistory = new List<TransactionItemModel>();
        }

        [JsonProperty(PropertyName = "transactionsHistory")]
        public List<TransactionItemModel> TransactionsHistory { get; set; }
    }

    public class TransactionItemModel
    {
        public TransactionItemModel()
        {
            this.Payments = new List<PaymentDetailModel>();
        }

        [JsonProperty(PropertyName = "type")]
        [JsonConverter(typeof(StringEnumConverter), true)]
        public TransactionItemType Type { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "toAddress", NullValueHandling = NullValueHandling.Ignore)]
        public string ToAddress { get; set; }

        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        [JsonProperty(PropertyName = "amount")]
        public Money Amount { get; set; }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public ICollection<PaymentDetailModel> Payments { get; set; }

        [JsonProperty(PropertyName = "fee", NullValueHandling = NullValueHandling.Ignore)]
        public Money Fee { get; set; }

        /// <summary>
        /// The height of the block in which this transaction was confirmed.
        /// </summary>
        [JsonProperty(PropertyName = "confirmedInBlock", NullValueHandling = NullValueHandling.Ignore)]
        public int? ConfirmedInBlock { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Timestamp { get; set; }
    }

    public class PaymentDetailModel
    {
        /// <summary>
        /// The Base58 representation of the destination  address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }
    }

    public enum TransactionItemType
    {
        Received,
        Send
    }
}
