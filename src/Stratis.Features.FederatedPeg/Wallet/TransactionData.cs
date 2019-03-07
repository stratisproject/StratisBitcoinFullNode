using System;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// An object containing transaction data.
    /// </summary>
    public class TransactionData
    {
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        [JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(BitcoinSerializableJsonConverter))]
        public PartialMerkleTree MerkleProof { get; set; }

        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Propagation state of this transaction.
        /// </summary>
        /// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
        [JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPropagated { get; set; }

        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails { get; set; }

        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        public Transaction GetFullTransaction(Network network)
        {
            return network.CreateTransaction(this.Hex);
        }

        public bool IsSpendable()
        {
            // TODO: Coinbase maturity check?
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (!this.IsSpendable()) return Money.Zero;
            
            if (confirmedOnly && !this.IsConfirmed())
            {
                return Money.Zero;
            }

            return this.Amount;

        }
    }
}