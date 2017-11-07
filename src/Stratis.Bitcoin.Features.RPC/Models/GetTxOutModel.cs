using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    /// <summary>
    /// A model returned to an RPC gettxout request
    /// </summary>
    public class GetTxOutModel
    {
        public GetTxOutModel()
        {
        }

        public GetTxOutModel(UnspentOutputs unspentOutputs, uint vout, Network network, ChainedBlock tip)
        {
            if(unspentOutputs != null)
            {
                var output = unspentOutputs.TryGetOutput(vout);
                this.bestblock = tip.HashBlock;
                this.coinbase = unspentOutputs.IsCoinbase;
                this.confirmations = NetworkExtensions.MempoolHeight == unspentOutputs.Height ? 0 : tip.Height - (int)unspentOutputs.Height + 1;
                if (output != null)
                {
                    this.value = output.Value;
                    this.scriptPubKey = new ScriptPubKey(output.ScriptPubKey, network);
                }
            }
        }

        [JsonProperty(Order = 0)]
        public uint256 bestblock { get; set; }

        [JsonProperty(Order = 1)]
        public int confirmations { get; set; }

        [JsonProperty(Order = 2)]
        public Money value { get; set; }

        [JsonProperty(Order = 3)]
        public ScriptPubKey scriptPubKey { get; set; }

        [JsonProperty(Order = 4)]
        public bool coinbase { get; set; }
    }
}
