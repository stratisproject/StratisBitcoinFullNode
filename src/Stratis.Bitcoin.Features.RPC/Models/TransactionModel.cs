using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.RPC.Converters;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public abstract class TransactionModel
    {
        public TransactionModel(Network network = null)
        {
        }

        public TransactionModel(Transaction trx)
        {
            this.Hex = trx?.ToHex();
        }

        [JsonProperty(Order = 0, PropertyName = "hex")]
        public string Hex { get; set; }

        public override string ToString()
        {
            return this.Hex;
        }
    }

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

    public class TransactionVerboseModel : TransactionModel
    {
        public TransactionVerboseModel()
        {
        }

        public TransactionVerboseModel(Transaction trx, Network network, ChainedBlock block = null, ChainedBlock tip = null) : base(trx)
        {
            if (trx != null)
            {
                this.TxId = trx.GetHash().ToString();
                this.Size = trx.GetSerializedSize();
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

        [JsonProperty(Order = 1, PropertyName = "txid")]
        public string TxId { get; set; }

        [JsonProperty(Order = 2, PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(Order = 3, PropertyName = "version")]
        public uint Version { get; set; }

        [JsonProperty(Order = 4, PropertyName = "locktime")]
        public uint LockTime { get; set; }

        [JsonProperty(Order = 5, PropertyName = "vin")]
        public List<Vin> VIn { get; set; }

        [JsonProperty(Order = 6, PropertyName = "vout")]
        public List<Vout> VOut { get; set; }

        [JsonProperty(Order = 7, PropertyName = "blockhash", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string BlockHash { get; set; }

        [JsonProperty(Order = 8, PropertyName = "confirmations", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Confirmations { get; set; }

        [JsonProperty(Order = 9, PropertyName = "time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? Time { get; set; }

        [JsonProperty(Order = 10, PropertyName = "blocktime", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? BlockTime { get; set; }
    }

    public class Vin
    {
        public Vin()
        {
        }

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

        [JsonProperty(Order = 0, PropertyName = "coinbase", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Coinbase { get; set; }

        [JsonProperty(Order = 1, PropertyName = "txid", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string TxId { get; set; }

        [JsonProperty(Order = 2, PropertyName = "vout", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? VOut { get; set; }

        [JsonProperty(Order = 3, PropertyName = "scriptSig", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script ScriptSig { get; set; }

        [JsonProperty(Order = 4, PropertyName = "sequence")]
        public uint Sequence { get; set; }
    }

    public class Vout
    {
        public Vout()
        {
        }

        public Vout(int N, TxOut txout, Network network)
        {
            this.N = N;
            this.Value = txout.Value.ToDecimal(MoneyUnit.BTC);
            this.ScriptPubKey = new ScriptPubKey(txout.ScriptPubKey, network);
        }

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 0, PropertyName = "value")]
        public decimal Value { get; set; }

        [JsonProperty(Order = 1, PropertyName = "n")]
        public int N { get; set; }

        [JsonProperty(Order = 2, PropertyName = "scriptPubKey")]
        public ScriptPubKey ScriptPubKey { get; set; }
    }

    public class Script
    {
        public Script()
        {
        }

        public Script(NBitcoin.Script script)
        {
            this.Asm = script.ToString();
            this.Hex = Encoders.Hex.EncodeData(script.ToBytes());
        }

        [JsonProperty(Order = 0, PropertyName = "asm")]
        public string Asm { get; set; }

        [JsonProperty(Order = 1, PropertyName = "hex")]
        public string Hex { get; set; }
    }

    public class ScriptPubKey : Script
    {
        public ScriptPubKey()
        {
        }

        public ScriptPubKey(NBitcoin.Script script, Network network) : base(script)
        {
            var destinations = new List<TxDestination> { script.GetDestination(network) };
            this.Type = this.GetScriptType(script.FindTemplate(network));
            if (destinations[0] == null)
            {
                destinations = script.GetDestinationPublicKeys(network)
                                    .Select(p => p.Hash)
                                    .ToList<TxDestination>();
            }
            else
            {
                if (destinations.Count == 1)
                {
                    this.ReqSigs = 1;
                    this.Addresses = new List<string> { destinations[0].GetAddress(network).ToString() };
                }
                else
                {
                    PayToMultiSigTemplateParameters multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(network, script);
                    this.ReqSigs = multi.SignatureCount;
                    this.Addresses = multi.PubKeys.Select(m => m.GetAddress(network).ToString()).ToList();
                }
            }
        }

        [JsonProperty(Order = 2, PropertyName = "reqSigs", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? ReqSigs { get; set; }

        [JsonProperty(Order = 3, PropertyName = "type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty(Order = 4, PropertyName = "addresses", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> Addresses { get; set; }

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
