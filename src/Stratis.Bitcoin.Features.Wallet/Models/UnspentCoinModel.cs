using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// Model for Json response for listunspent RPC call.
    /// </summary>
    public class UnspentCoinModel
    {
        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "txid")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        /// <summary>
        /// The index of the output in the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "vout")]
        public int Index { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// Account name.
        /// </summary>
        [JsonProperty(PropertyName = "account")]
        public string Account { get; set; }

        /// <summary>
        /// The output script paid, encoded as hex.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        public string ScriptPubKeyHex { get; set; }

        /// <summary>
        /// If the output is a P2SH whose script belongs to this wallet, this is the redeem script.
        /// </summary>
        [JsonProperty(PropertyName = "redeemScript")]
        public string RedeemScriptHex { get; set; }

        /// <summary>
        /// The transaction amount.
        /// Serialized in coins (BTC).
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyInCoinsJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// The number of confirmations.
        /// </summary>
        [JsonProperty(PropertyName = "confirmations")]
        public int Confirmations { get; set; }

        /// <summary>
        /// Whether the private key or keys needed to spend this output are part of the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "spendable")]
        public bool IsSpendable { get; set; }

        /// <summary>
        /// Whether the wallet knows how to spend this output.
        /// </summary>
        [JsonProperty(PropertyName = "solvable")]
        public bool IsSolvable { get; set; }
    }
}
