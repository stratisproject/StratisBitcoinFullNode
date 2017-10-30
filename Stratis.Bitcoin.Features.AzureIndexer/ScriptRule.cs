using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class ScriptRule : WalletRule
    {
        public ScriptRule(Script destination, Script redeemScript = null)
        {
            ScriptPubKey = destination;
            RedeemScript = redeemScript;
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
                return ScriptPubKey.Hash.ToString();
            }
        }
    }
}
