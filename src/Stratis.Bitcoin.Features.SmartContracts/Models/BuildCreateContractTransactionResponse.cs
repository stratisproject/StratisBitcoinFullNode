using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class BuildCreateContractTransactionResponse
    {
        [JsonProperty(PropertyName = "fee")]
        public Money Fee { get; set; }

        [JsonProperty(PropertyName= "hex")]
        public string Hex { get; set; }

        [JsonProperty(PropertyName = "transactionId")]
        public uint256 TransactionId { get; set; }

        [JsonProperty(PropertyName ="newContractAddress")]
        public string NewContractAddress { get; set; }
    }
}
