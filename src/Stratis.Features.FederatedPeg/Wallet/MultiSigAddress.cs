using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// A multi-signature address; where only our private keys are derived from an HD seed.
    /// </summary>
    public class MultiSigAddress
    {
        public MultiSigAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// The script pub key for this address. This is the P2SH version incorporating all the signatories.
        /// This is the hashed version of the redeem script. In the context of a P2SH transaction this is
        /// what is conventionally referred to as the scriptPubKey, even though it is only used for receiving
        /// funds and cannot be used for spending them.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The number of signatories required for a multisig transaction to be signed.
        /// </summary>
        [JsonProperty(PropertyName = "m")]
        public int M { get; set; }

        /// <summary>
        /// The pubkeys for this address, from all signatories.
        /// </summary>
        [JsonProperty(PropertyName = "pubkeys")]
        public ICollection<Script> Pubkeys { get; set; }

        /// <summary>
        /// The redeemScript for this address. Needed for P2SH multisig as the full script is not present
        /// in the previous transaction output(s). This is the full 'scriptPubKey' of the multisig address.
        /// It is needed when the signatories wish to spend funds held in the multisig address.
        /// </summary>
        [JsonProperty(PropertyName = "redeemScript")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script RedeemScript{ get; set; }

        /// <summary>
        /// The Base58 representation of this address. The address is effectively derived from the
        /// PaymentScript. It is possible to re-derive the scriptPubKey for receiving payments from the
        /// address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A list of transactions involving this multisig address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }
        
        public Key GetPrivateKey(string encryptedSeed, string password, Network network)
        {
            return Key.Parse(encryptedSeed, password, network);
        }

        /// <summary>
        /// List all spendable transactions in a multisig address.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => t.IsSpendable());
        }
    }
}
