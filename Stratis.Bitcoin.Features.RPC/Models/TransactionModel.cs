using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.RPC.Converters;

#pragma warning disable IDE1006 // Naming Styles (ignore lowercase)
namespace Stratis.Bitcoin.Features.RPC.Models
{

    public abstract class TransactionModel
    {
        public TransactionModel(Network network = null) { }

        public TransactionModel(NBitcoin.Transaction trx)
        {
            this.hex = trx?.ToHex();
        }

        [JsonProperty(Order = 0)]
        public string hex { get; set; }

        public override string ToString()
        {
            return this.hex;
        }
    }

    [JsonConverter(typeof(ToStringJsonConverter))]
    public class TransactionBriefModel : TransactionModel
    {
        public TransactionBriefModel() { }
        public TransactionBriefModel(NBitcoin.Transaction trx) : base(trx) { }
    }

    public class TransactionVerboseModel : TransactionModel
    {

        public TransactionVerboseModel() { }

        public TransactionVerboseModel(NBitcoin.Transaction trx, Network network, NBitcoin.ChainedBlock block = null, ChainedBlock tip = null) : base(trx)
        {
            if (trx != null)
            {
                this.txid = trx.GetHash().ToString();
                this.size = trx.GetSerializedSize();
                this.version = trx.Version;
                this.locktime = trx.LockTime;

                this.vin = trx.Inputs.Select(txin => new Vin(txin.PrevOut, txin.Sequence, txin.ScriptSig)).ToList();

                int n = 0;
                this.vout = trx.Outputs.Select(txout => new Vout(n++, txout, network)).ToList();

                if (block != null)
                {
                    this.blockhash = block.HashBlock.ToString();
                    this.time = this.blocktime = Utils.DateTimeToUnixTime(block.Header.BlockTime);
                    if (tip != null)
                        this.confirmations = tip.Height - block.Height + 1;
                }
            }

        }

        [JsonProperty(Order = 1)]
        public string txid { get; set; }

        [JsonProperty(Order = 2)]
        public int size { get; set; }

        [JsonProperty(Order = 3)]
        public uint version { get; set; }

        [JsonProperty(Order = 4)]
        public uint locktime { get; set; }

        [JsonProperty(Order = 5)]
        public List<Vin> vin { get; set; }

        [JsonProperty(Order = 6)]
        public List<Vout> vout { get; set; }

        [JsonProperty(Order = 7, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string blockhash { get; set; }

        [JsonProperty(Order = 8, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? confirmations { get; set; }

        [JsonProperty(Order = 9, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? time { get; set; }

        [JsonProperty(Order = 10, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? blocktime { get; set; }
    }

    public class Vin
    {
        public Vin() { }

        public Vin(NBitcoin.OutPoint prevOut, NBitcoin.Sequence sequence, NBitcoin.Script scriptSig)
        {

            if (prevOut.Hash == uint256.Zero)
            {
                this.coinbase = Encoders.Hex.EncodeData(scriptSig.ToBytes());
            }
            else
            {
                this.txid = prevOut.Hash.ToString();
                this.vout = prevOut.N;
                this.scriptSig = new Script(scriptSig); ;
            }
            this.sequence = (uint)sequence;

        }

        [JsonProperty(Order = 0, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string coinbase { get; set; }

        [JsonProperty(Order = 1, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string txid { get; set; }

        [JsonProperty(Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? vout { get; set; }

        [JsonProperty(Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script scriptSig { get; set; }

        [JsonProperty(Order = 4)]
        public uint sequence { get; set; }
    }

    public class Vout
    {
        public Vout() { }

        public Vout(int N, TxOut txout, Network network)
        {
            this.n = N;
            this.value = txout.Value.ToDecimal(MoneyUnit.BTC);
            this.scriptPubKey = new ScriptPubKey(txout.ScriptPubKey, network);
        }

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        [JsonProperty(Order = 0)]
        public decimal value { get; set; }

        [JsonProperty(Order = 1)]
        public int n { get; set; }

        [JsonProperty(Order = 2)]
        public ScriptPubKey scriptPubKey { get; set; }
    }

    public class Script
    {
        public Script() { }

        public Script(NBitcoin.Script script)
        {
            this.asm = script.ToString();
            this.hex = Encoders.Hex.EncodeData(script.ToBytes());
        }


        [JsonProperty(Order = 0)]
        public string asm { get; set; }

        [JsonProperty(Order = 1)]
        public string hex { get; set; }

    }

    public class ScriptPubKey : Script
    {
        public ScriptPubKey() { }

        public ScriptPubKey(NBitcoin.Script script, Network network) : base(script)
        {
            var destinations = new List<TxDestination> { script.GetDestination() };
            this.type = this.GetScriptType(script.FindTemplate());
            if (destinations[0] == null)
            {
                destinations = script.GetDestinationPublicKeys()
                                    .Select(p => p.Hash)
                                    .ToList<TxDestination>();
            }
            else
            {
                if (destinations.Count == 1)
                {
                    this.reqSigs = 1;
                    this.addresses = new List<string> { destinations[0].GetAddress(network).ToString() };
                }
                else
                {
                    PayToMultiSigTemplateParameters multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(script);
                    this.reqSigs = multi.SignatureCount;
                    this.addresses = multi.PubKeys.Select(m => m.GetAddress(network).ToString()).ToList();
                }
            }
        }

        [JsonProperty(Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? reqSigs { get; set; }

        [JsonProperty(Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string type { get; set; }

        [JsonProperty(Order = 4, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> addresses { get; set; }

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
#pragma warning restore IDE1006 // Naming Styles
