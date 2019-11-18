using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// An object representing a payment.
    /// </summary>
    public class PaymentDetails
    {
        /// <summary>
        /// The script pub key of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script DestinationScriptPubKey { get; set; }

        /// <summary>
        /// The Base58 representation of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The index of the output of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "outputIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? OutputIndex { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }
    }
}