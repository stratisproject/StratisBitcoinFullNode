using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC
{
    public class SignRawTransactionResponse
    {
        [JsonProperty(PropertyName = "hex")]
        public Transaction Transaction
        {
            get; set;
        }

        [JsonProperty(PropertyName = "complete")]
        public bool Complete
        {
            get; set;
        }

        // TODO: Add errors array
    }
}
