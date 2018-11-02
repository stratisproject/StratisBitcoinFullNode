using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public class ContractTransactionItemModel
    {
        public ContractTransactionItemModel()
        {
            this.Payments = new List<PaymentDetailModel>();
        }

        [JsonProperty(PropertyName = "type")]
        [JsonConverter(typeof(StringEnumConverter), true)]
        public ContractTransactionItemType Type { get; set; }

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
}
