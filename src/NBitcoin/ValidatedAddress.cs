using Newtonsoft.Json;

namespace NBitcoin
{
    public class ValidatedAddress
    {
        [JsonProperty(PropertyName = "isvalid")]
        public bool IsValid { get; set; }
    }
}
