using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Wallet
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
}