using NBitcoin;
using NBitcoin.Formatters;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Features.RPC
{
    public class UnspentTransaction
    {
        public UnspentTransaction(JObject unspent)
        {
            this.bestblock = uint256.Parse((string)unspent[nameof(this.bestblock)]);
            this.confirmations = (int)unspent[(nameof(this.confirmations))];
            this.value = (decimal)unspent[(nameof(this.value))];
            this.scriptPubKey = unspent[nameof(this.scriptPubKey)].ToObject<RPCScriptPubKey>();
            this.coinbase = (bool)unspent[(nameof(this.coinbase))];
        }

        public uint256 bestblock { get; set; }
        public int confirmations { get; set; }
        public decimal value { get; set; }
        public RPCScriptPubKey scriptPubKey { get; set; }
        public bool coinbase { get; set; }
    }
}
