using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A model returned by a gettxout request
    /// </summary>
    public class GetTxOutModel
    {
        public GetTxOutModel()
        {
        }

        /// <summary>
        /// Initializes a GetTxOutModel instance.
        /// </summary>
        /// <param name="unspentOutputs">The <see cref="UnspentOutputs"/>.</param>
        /// <param name="vout">The output index.</param>
        /// <param name="network">The network the transaction occurred on.</param>
        /// <param name="tip">The current consensus tip's <see cref="ChainedHeader"/>.</param>
        public GetTxOutModel(UnspentOutputs unspentOutputs, uint vout, Network network, ChainedHeader tip)
        {
            if (unspentOutputs != null)
            {
                TxOut output = unspentOutputs.TryGetOutput(vout);
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

        /// <summary>The block hash of the consensus tip.</summary>
        [JsonProperty(Order = 0, PropertyName = "bestblock")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BestBlock { get; set; }

        /// <summary>The number of confirmations for the unspent output.</summary>
        [JsonProperty(Order = 1, PropertyName = "confirmations")]
        public int Confirmations { get; set; }

        /// <summary>The value of the output.</summary>
        [JsonProperty(Order = 2, PropertyName = "value")]
        public Money Value { get; set; }

        /// <summary>The output's <see cref="ScriptPubKey"/></summary>
        [JsonProperty(Order = 3, PropertyName = "scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }

        /// <summary>Boolean indicating if the unspent output is a coinbase transaction.</summary>
        [JsonProperty(Order = 4, PropertyName = "coinbase")]
        public bool Coinbase { get; set; }
    }
}
