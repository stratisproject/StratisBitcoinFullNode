﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NBitcoin.Formatters
{
    internal class SatoshiFormatter : RawFormatter
    {
        public SatoshiFormatter(Network network) : base(network)
        {
        }

        protected override void BuildTransaction(JObject json, Transaction tx)
        {
            tx.Version = (uint)json.GetValue("version");
            tx.LockTime = (uint)json.GetValue("locktime");

            var vin = (JArray)json.GetValue("vin");
            for(int i = 0; i < vin.Count; i++)
            {
                var jsonIn = (JObject)vin[i];
                var txin = new TxIn();
                tx.Inputs.Add(txin);

                var script = (JObject)jsonIn.GetValue("scriptSig");
                if(script != null)
                {
                    txin.ScriptSig = new Script(Encoders.Hex.DecodeData((string)script.GetValue("hex")));
                    txin.PrevOut.Hash = uint256.Parse((string)jsonIn.GetValue("txid"));
                    txin.PrevOut.N = (uint)jsonIn.GetValue("vout");
                }
                else
                {
                    string coinbase = (string)jsonIn.GetValue("coinbase");
                    txin.ScriptSig = new Script(Encoders.Hex.DecodeData(coinbase));
                }

                txin.Sequence = (uint)jsonIn.GetValue("sequence");

            }

            var vout = (JArray)json.GetValue("vout");
            for(int i = 0; i < vout.Count; i++)
            {
                var jsonOut = (JObject)vout[i];
                var txout = new TxOut();
                tx.Outputs.Add(txout);

                decimal btc = (decimal)jsonOut.GetValue("value");
                decimal satoshis = btc * Money.COIN;
                txout.Value = new Money((long)(satoshis));

                var script = (JObject)jsonOut.GetValue("scriptPubKey");
                txout.ScriptPubKey = new Script(Encoders.Hex.DecodeData((string)script.GetValue("hex")));
            }
        }

        protected override void WriteTransaction(JsonTextWriter writer, Transaction tx)
        {
            WritePropertyValue(writer, "txid", tx.GetHash().ToString());
            WritePropertyValue(writer, "version", tx.Version);
            WritePropertyValue(writer, "locktime", tx.LockTime.Value);

            writer.WritePropertyName("vin");
            writer.WriteStartArray();
            foreach(TxIn txin in tx.Inputs)
            {
                writer.WriteStartObject();

                if(txin.PrevOut.Hash == uint256.Zero)
                {
                    WritePropertyValue(writer, "coinbase", Encoders.Hex.EncodeData(txin.ScriptSig.ToBytes()));
                }
                else
                {
                    WritePropertyValue(writer, "txid", txin.PrevOut.Hash.ToString());
                    WritePropertyValue(writer, "vout", txin.PrevOut.N);
                    writer.WritePropertyName("scriptSig");
                    writer.WriteStartObject();

                    WritePropertyValue(writer, "asm", txin.ScriptSig.ToString());
                    WritePropertyValue(writer, "hex", Encoders.Hex.EncodeData(txin.ScriptSig.ToBytes()));

                    writer.WriteEndObject();
                }
                WritePropertyValue(writer, "sequence", (uint)txin.Sequence);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("vout");
            writer.WriteStartArray();

            int i = 0;
            foreach(TxOut txout in tx.Outputs)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("value");
                writer.WriteRawValue(ValueFromAmount(txout.Value));
                WritePropertyValue(writer, "n", i);

                writer.WritePropertyName("scriptPubKey");
                writer.WriteStartObject();

                WritePropertyValue(writer, "asm", txout.ScriptPubKey.ToString());
                WritePropertyValue(writer, "hex", Encoders.Hex.EncodeData(txout.ScriptPubKey.ToBytes()));

                var destinations = new List<TxDestination>() { txout.ScriptPubKey.GetDestination(this.Network) };
                if(destinations[0] == null)
                {
                    destinations = txout.ScriptPubKey.GetDestinationPublicKeys(this.Network)
                                                        .Select(p => p.Hash)
                                                        .ToList<TxDestination>();
                }
                if(destinations.Count == 1)
                {
                    WritePropertyValue(writer, "reqSigs", 1);
                    WritePropertyValue(writer, "type", GetScriptType(txout.ScriptPubKey.FindTemplate(this.Network)));
                    writer.WritePropertyName("addresses");
                    writer.WriteStartArray();
                    writer.WriteValue(destinations[0].GetAddress(this.Network).ToString());
                    writer.WriteEndArray();
                }
                else
                {
                    PayToMultiSigTemplateParameters multi = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
                    if (multi != null)
                        WritePropertyValue(writer, "reqSigs", multi.SignatureCount);
                    WritePropertyValue(writer, "type", GetScriptType(txout.ScriptPubKey.FindTemplate(this.Network)));
                    if (multi != null)
                    {
                        writer.WritePropertyName("addresses");
                        writer.WriteStartArray();
                        foreach (PubKey key in multi.PubKeys)
                        {
                            writer.WriteValue(key.Hash.GetAddress(this.Network).ToString());
                        }
                        writer.WriteEndArray();
                    }
                }

                writer.WriteEndObject(); //endscript
                writer.WriteEndObject(); //in out
                i++;
            }
            writer.WriteEndArray();
        }

        private string ValueFromAmount(Money money)
        {
            decimal satoshis = (decimal)money.Satoshi;
            decimal btc = satoshis / Money.COIN;
            //return btc.ToString("0.###E+00", CultureInfo.InvariantCulture);
            string result = ((double)btc).ToString(CultureInfo.InvariantCulture);
            if(!result.ToCharArray().Contains('.'))
                result = result + ".0";
            return result;
        }

        private string GetScriptType(ScriptTemplate template)
        {
            if(template == null)
                return "nonstandard";
            switch(template.Type)
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
