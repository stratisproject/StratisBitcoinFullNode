using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
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
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The path to this wallet's portion of the multisig key (see BIP45).
        /// m / purpose' / cosigner_index / change / address_index
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

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

        public void Create(int index, KeyPath keyPath, ExtPubKey[] extPubKeys, int M, Network network)
        {
            this.Index = index;

            this.HdPath = keyPath.ToString();

            // FIXME: For BIP45 the keys need to be lexicographically sorted, but ExtPubKeys do not implement IComparable
            //Array.Sort(extPubKeys);

            List<PubKey> pubKeys = new List<PubKey>();

            foreach (var extPubKey in extPubKeys)
            {
                // Get the BIP45 public key for each cosigner at the desired index.
                // For simplicity only the first cosigner's branch is used at present;
                // a fully BIP45 compatible implementation will use a particular
                // branch depending on whether a particular cosigner needs to send or
                // receive funds. Only one address presently needs to be used for
                // a federation's 'receive' transactions, although this can be extended
                // going forwards.

                pubKeys.Add(extPubKey.Derive(45).Derive(0).Derive(0).Derive((uint)index).PubKey);
            }

            // M-of-N multisig, where N is implicitly the number of PubKeys in the array
            Script scriptPubKey = PayToMultiSigTemplate
                .Instance
                .GenerateScriptPubKey(M, pubKeys.ToArray());

            this.M = M;

            this.Pubkeys = new List<Script>();

            foreach (var pubKey in pubKeys)
            {
                this.Pubkeys.Add(pubKey.ScriptPubKey);
            }

            this.RedeemScript = scriptPubKey;

            this.ScriptPubKey = scriptPubKey.PaymentScript;

            this.Address = scriptPubKey.Hash.GetAddress(network).ToString();
        }

        public Key GetPrivateKey(string encryptedSeed, byte[] chainCode, string password, Network network)
        {
            Key key = MultiSigHdOperations.DecryptSeed(encryptedSeed, password, network);
            var extKey = MultiSigHdOperations.GetExtendedPrivateKey(key, chainCode, this.HdPath, network);

            return extKey.PrivateKey;
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

        /// <summary>
        /// Parts of the TransactionBuilder, e.g. the change address handling, require
        /// a GeneralPurposeAddress to function. This method is a temporary workaround
        /// until the logic can be abstracted to work with any address type. Does not
        /// include private key material.
        /// </summary>
        public GeneralPurposeAddress AsGeneralPurposeAddress()
        {
            var address = new GeneralPurposeAddress()
            {
                Address = this.Address,
                ScriptPubKey = this.ScriptPubKey
            };

            return address;
        }
    }
}
