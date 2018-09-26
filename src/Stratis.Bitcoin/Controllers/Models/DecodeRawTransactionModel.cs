using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>Contains a transaction in hex format prior to being decoded to JSON.</summary>
    public class DecodeRawTransactionModel
    {
        /// <summary>The transaction to be decoded, in hex format.</summary>
        [JsonProperty(PropertyName = "rawHex")]
        public string RawHex { get; set; }
    }
}
