using Newtonsoft.Json;

namespace Stratis.Bitcoin.NBitcoin
{
    public class ValidatedAddress
    {
        [JsonProperty(PropertyName = "isvalid")]
        public bool IsValid { get; set; }
    }
}
