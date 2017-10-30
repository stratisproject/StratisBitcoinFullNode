using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.AzureIndexer
{
    public abstract class WalletRule
    {
        public WalletRule()
        {
        }

        [JsonIgnore]
        public abstract string Id
        {
            get;
        }

        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]
        public string CustomData
        {
            get;
            set;
        }

        public override string ToString()
        {
            return Helper.Serialize(this);
        }
    }
}
