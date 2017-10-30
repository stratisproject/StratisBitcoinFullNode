using Newtonsoft.Json;
using NBitcoin;

namespace Stratis.Bitcoin.Features.AzureIndexer
{
    public class ScriptRule : WalletRule
    {
        public ScriptRule(Script destination, Script redeemScript = null)
        {
            this.ScriptPubKey = destination;
            this.RedeemScript = redeemScript;
        }

        public ScriptRule(IDestination destination, Script redeemScript = null)
            : this(destination.ScriptPubKey, redeemScript)
        {
        }

        public ScriptRule()
        {
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script ScriptPubKey
        {
            get;
            set;
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script RedeemScript
        {
            get;
            set;
        }

        public override string Id
        {
            get
            {
                return this.ScriptPubKey.Hash.ToString();
            }
        }
    }
}
