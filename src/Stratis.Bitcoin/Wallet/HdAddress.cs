using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;
using TracerAttributes;

namespace Stratis.Bitcoin.Wallet
{
    /// <summary>
    /// An HD address.
    /// </summary>
    public class HdAddress
    {
        public HdAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The P2PKH (pay-to-pubkey-hash) script pub key for this address.
        /// </summary>
        /// <remarks>The script is of the format OP_DUP OP_HASH160 {pubkeyhash} OP_EQUALVERIFY OP_CHECKSIG</remarks>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The P2PK (pay-to-pubkey) script pub key corresponding to the private key of this address.
        /// </summary>
        /// <remarks>This is typically only used for mining, as the valid script types for mining are constrained.
        /// Block explorers often depict the P2PKH address as the 'address' of a P2PK scriptPubKey, which is not
        /// actually correct. A P2PK scriptPubKey does not have a defined address format.
        /// 
        /// The script itself is of the format: {pubkey} OP_CHECKSIG</remarks>
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        [NoTrace]
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns>List of spendable transactions.</returns>
        [NoTrace]
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => !t.IsSpent());
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetBalances()
        {
            List<TransactionData> allTransactions = this.Transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.GetUnspentAmount(true));
            long total = allTransactions.Sum(t => t.GetUnspentAmount(false));

            return (confirmed, total - confirmed);
        }
    }
}