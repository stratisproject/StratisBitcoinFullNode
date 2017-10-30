using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
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
