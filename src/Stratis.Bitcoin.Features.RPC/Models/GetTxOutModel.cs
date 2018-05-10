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

        public GetTxOutModel(UnspentOutputs unspentOutputs, uint vout, Network network, ChainedHeader tip)
        {
            if (unspentOutputs != null)
            {
                var output = unspentOutputs.TryGetOutput(vout);
                this.BestBlock = tip.HashBlock;
                this.Coinbase = unspentOutputs.IsCoinbase;
                this.Confirmations = NetworkExtensions.MempoolHeight == unspentOutputs.Height ? 0 : tip.Height - (int)unspentOutputs.Height + 1;
                if (output != null)
                {
                    this.Value = output.Value;
                    this.ScriptPubKey = new ScriptPubKey(output.ScriptPubKey, network);
                }
            }
        }

        [JsonProperty(Order = 0, PropertyName = "bestblock")]
        public uint256 BestBlock { get; set; }

        [JsonProperty(Order = 1, PropertyName = "confirmations")]
        public int Confirmations { get; set; }

        [JsonProperty(Order = 2, PropertyName = "value")]
        public Money Value { get; set; }

        [JsonProperty(Order = 3, PropertyName = "scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }

        [JsonProperty(Order = 4, PropertyName = "coinbase")]
        public bool Coinbase { get; set; }
    }
}
