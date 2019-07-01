using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public sealed class GetCodeResponse
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "bytecode")]
        public string Bytecode { get; set; }

        [JsonProperty(PropertyName = "csharp")]
        public string CSharp { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}