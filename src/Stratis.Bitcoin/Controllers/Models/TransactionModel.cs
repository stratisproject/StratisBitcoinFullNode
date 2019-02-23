using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Controllers.Converters;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A class representing a transaction.
    /// </summary>
    public abstract class TransactionModel
    {
        public TransactionModel(Network network = null)
        {
        }

        /// <summary>
        /// Creates a <see cref="TransactionModel"/> containing the hash of the given transaction.
        /// </summary>
        /// <param name="trx">A valid <see cref="Transaction"/></param>
        public TransactionModel(Transaction trx)
        {
            this.Hex = trx?.ToHex();
        }

        /// <summary>The hashed transaction.</summary>
        [JsonProperty(Order = 0, PropertyName = "hex")]
        public string Hex { get; set; }

        public override string ToString()
        {
            return this.Hex;
        }
    }

    /// <summary>
    /// Creates a concise transaction model containing the hashed transaction.
    /// </summary>
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class TransactionBriefModel : TransactionModel
    {
        public TransactionBriefModel()
        {
        }

        public TransactionBriefModel(Transaction trx) : base(trx)
        {
        }
    }

    /// <summary>
    /// Creates a more robust transaction model.
    /// </summary>
    public class TransactionVerboseModel : TransactionModel
    {
        public TransactionVerboseModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionVerboseModel"/> class.
        /// </summary>
        /// <param name="trx">The transaction.</param>
        /// <param name="network">The network the transaction occurred on.</param>
        /// <param name="block">A <see cref="ChainedHeader"/> of the block that contains the transaction.</param>
        /// <param name="tip">A <see cref="ChainedHeader"/> of the current tip.</param>
        public TransactionVerboseModel(Transaction trx, Network network, ChainedHeader block = null, ChainedHeader tip = null) : base(trx)
        {
            if (trx != null)
            {
                this.TxId = trx.GetHash().ToString();
                this.Hash = trx.HasWitness ? trx.GetWitHash().ToString() : trx.GetHash().ToString();
                this.Size = trx.GetSerializedSize();
                this.VSize = trx.HasWitness ? trx.GetVirtualSize() : trx.GetSerializedSize();
                this.Version = trx.Version;
                this.LockTime = trx.LockTime;

                this.VIn = trx.Inputs.Select(txin => new Vin(txin.PrevOut, txin.Sequence, txin.ScriptSig)).ToList();

                int n = 0;
                this.VOut = trx.Outputs.Select(txout => new Vout(n++, txout, network)).ToList();

                if (block != null)
                {
                    this.BlockHash = block.HashBlock.ToString();
                    this.Time = this.BlockTime = Utils.DateTimeToUnixTime(block.Header.BlockTime);
                    if (tip != null)
                        this.Confirmations = tip.Height - block.Height + 1;
                }
            }
        }

        /// <summary>The transaction id.</summary>
        [JsonProperty(Order = 1, PropertyName = "txid")]
        public string TxId { get; set; }

        /// <summary>The transaction hash (differs from txid for witness transactions).</summary>
        [JsonProperty(Order = 2, PropertyName = "hash")]
        public string Hash { get; set; }

        /// <summary>The transaction version number (typically 1).</summary>
        [JsonProperty(Order = 3, PropertyName = "version")]
        public uint Version { get; set; }

        /// <summary>The serialized transaction size.</summary>
        [JsonProperty(Order = 4, PropertyName = "size")]
        public int Size { get; set; }

        /// <summary>The virtual transaction size (differs from size for witness transactions).</summary>
        [JsonProperty(Order = 5, PropertyName = "vsize")]
        public int VSize { get; set; }

        /// <summary>The transaction's weight (between vsize*4-3 and vsize*4).</summary>
        [JsonProperty(Order = 6, PropertyName = "weight", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Weight { get; set; }

        /// <summary>If nonzero, block height or timestamp when transaction is final.</summary>
        [JsonProperty(Order = 7, PropertyName = "locktime")]
        public uint LockTime { get; set; }

        /// <summary>A list of inputs.</summary>
        [JsonProperty(Order = 8, PropertyName = "vin")]
        public List<Vin> VIn { get; set; }

        /// <summary>A list of outputs.</summary>
        [JsonProperty(Order = 9, PropertyName = "vout")]
        public List<Vout> VOut { get; set; }

        /// <summary>The hash of the block containing this transaction.</summary>
        [JsonProperty(Order = 10, PropertyName = "blockhash", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string BlockHash { get; set; }

        /// <summary>The number of confirmations of the transaction.</summary>
        [JsonProperty(Order = 11, PropertyName = "confirmations", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Confirmations { get; set; }

        /// <summary>The time the transaction was added to a block.</summary>
        [JsonProperty(Order = 12, PropertyName = "time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? Time { get; set; }

        /// <summary>The time the block was confirmed.</summary>
        [JsonProperty(Order = 13, PropertyName = "blocktime", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? BlockTime { get; set; }
    }

    /// <summary>
    /// A class describing a transaction input.
    /// </summary>
    public class Vin
    {
        public Vin()
        {
        }

        /// <summary>
        /// Initializes a <see cref="Vin"/> instance.
        /// </summary>
        /// <param name="prevOut">The previous output being used as an input.</param>
        /// <param name="sequence">The transaction's sequence number.</param>
        /// <param name="scriptSig">The scriptSig</param>
        public Vin(OutPoint prevOut, Sequence sequence, NBitcoin.Script scriptSig)
        {
            if (prevOut.Hash == uint256.Zero)
            {
                this.Coinbase = Encoders.Hex.EncodeData(scriptSig.ToBytes());
            }
            else
            {
                this.TxId = prevOut.Hash.ToString();
                this.VOut = prevOut.N;
                this.ScriptSig = new Script(scriptSig);
            }

            this.Sequence = (uint)sequence;
        }

        /// <summary>The scriptsig if this was a coinbase transaction.</summary>
        [JsonProperty(Order = 0, PropertyName = "coinbase", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Coinbase { get; set; }

        /// <summary>The transaction ID.</summary>
        [JsonProperty(Order = 1, PropertyName = "txid", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string TxId { get; set; }

        /// <summary>The index of the output.</summary>
        [JsonProperty(Order = 2, PropertyName = "vout", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? VOut { get; set; }

        /// <summary>The transaction's scriptsig.</summary>
        [JsonProperty(Order = 3, PropertyName = "scriptSig", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script ScriptSig { get; set; }

        /// <summary>The transaction's sequence number. <see cref="https://bitcoin.org/en/developer-guide#locktime-and-sequence-number"/></summary>
        [JsonProperty(Order = 4, PropertyName = "sequence")]
        public uint Sequence { get; set; }
    }

    /// <summary>
    /// A class describing a transaction output.
    /// </summary>
    public class Vout
    {
        public Vout()
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="Vout"/> class.
        /// </summary>
        /// <param name="n">The index of the output.</param>
        /// <param name="txout">A <see cref="TxOut"/></param>
        /// <param name="network">The network where the transaction occured.</param>
        public Vout(int n, TxOut txout, Network network)
        {
            this.N = n;
            this.Value = txout.Value.ToDecimal(MoneyUnit.BTC);
            this.ScriptPubKey = new ScriptPubKey(txout.ScriptPubKey, network);
        }

        /// <summary>The value of the output.</summary>
        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 0, PropertyName = "value")]
        public decimal Value { get; set; }

        /// <summary>The index of the output.</summary>
        [JsonProperty(Order = 1, PropertyName = "n")]
        public int N { get; set; }

        /// <summary>The output's scriptpubkey.</summary>
        [JsonProperty(Order = 2, PropertyName = "scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }
    }

    /// <summary>
    /// A class describing a transaction script.
    /// </summary>
    public class Script
    {
        public Script()
        {
        }

        /// <summary>
        /// Initializes a transaction <see cref="Script"/>, which contains the assembly and a hexadecimal representation of the script.
        /// </summary>
        /// <param name="script">A <see cref="NBitcoin.Script"/>.</param>
        public Script(NBitcoin.Script script)
        {
            this.Asm = script.ToString();
            this.Hex = Encoders.Hex.EncodeData(script.ToBytes());
        }

        /// <summary>The script's assembly.</summary>
        [JsonProperty(Order = 0, PropertyName = "asm")]
        public string Asm { get; set; }

        /// <summary>A hexadecimal representation of the script.</summary>
        [JsonProperty(Order = 1, PropertyName = "hex")]
        public string Hex { get; set; }
    }

    /// <summary>
    /// A class describing a ScriptPubKey.
    /// </summary>
    public class ScriptPubKey : Script
    {
        public ScriptPubKey()
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="ScriptPubKey"/> class.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="network">The network where the transaction was conducted.</param>
        public ScriptPubKey(NBitcoin.Script script, Network network) : base(script)
        {
            var destinations = new List<TxDestination> { script.GetDestination(network) };
            this.Type = this.GetScriptType(script.FindTemplate(network));

            if (destinations[0] == null)
            {
                destinations = script.GetDestinationPublicKeys(network).Select(p => p.Hash).ToList<TxDestination>();
            }

            if (destinations.Count == 1)
            {
                this.ReqSigs = 1;
                this.Addresses = new List<string> { destinations[0].GetAddress(network).ToString() };
            }
            else if (destinations.Count > 1)
            {
                PayToMultiSigTemplateParameters multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(script);
                this.ReqSigs = multi.SignatureCount;
                this.Addresses = multi.PubKeys.Select(m => m.GetAddress(network).ToString()).ToList();
            }
        }

        /// <summary>The number of required sigs.</summary>
        [JsonProperty(Order = 2, PropertyName = "reqSigs", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? ReqSigs { get; set; }

        /// <summary>The type of script.</summary>
        [JsonProperty(Order = 3, PropertyName = "type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Type { get; set; }

        /// <summary>A list of output addresses.</summary>
        [JsonProperty(Order = 4, PropertyName = "addresses", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> Addresses { get; set; }

        /// <summary>
        /// A method that returns a script type description.
        /// </summary>
        /// <param name="template">A <see cref="ScriptTemplate"/> used for the script.</param>
        /// <returns>A string describin the script type.</returns>
        protected string GetScriptType(ScriptTemplate template)
        {
            if (template == null)
                return "nonstandard";
            switch (template.Type)
            {
                case TxOutType.TX_PUBKEY:
                    return "pubkey";

                case TxOutType.TX_PUBKEYHASH:
                    return "pubkeyhash";

                case TxOutType.TX_SCRIPTHASH:
                    return "scripthash";

                case TxOutType.TX_MULTISIG:
                    return "multisig";

                case TxOutType.TX_NULL_DATA:
                    return "nulldata";
            }

            return "nonstandard";
        }
    }
}
