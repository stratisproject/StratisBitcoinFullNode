using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NBitcoin.Formatters
{
    internal class BlockExplorerFormatter : RawFormatter
    {
        internal BlockExplorerFormatter(Network network) : base(network)
        {
        }

        protected override void BuildTransaction(JObject json, Transaction tx)
        {
            tx.Version = (uint)json.GetValue("ver");
            tx.LockTime = (uint)json.GetValue("lock_time");

            var vin = (JArray)json.GetValue("in");
            int vinCount = (int)json.GetValue("vin_sz");
            for(int i = 0; i < vinCount; i++)
            {
                var jsonIn = (JObject)vin[i];
                var txin = new TxIn();
                tx.Inputs.Add(txin);
                var prevout = (JObject)jsonIn.GetValue("prev_out");

                txin.PrevOut.Hash = uint256.Parse((string)prevout.GetValue("hash"));
                txin.PrevOut.N = (uint)prevout.GetValue("n");


                string script = (string)jsonIn.GetValue("scriptSig");
                if(script != null)
                {
                    txin.ScriptSig = new Script(script);
                }
                else
                {
                    string coinbase = (string)jsonIn.GetValue("coinbase");
                    txin.ScriptSig = new Script(Encoders.Hex.DecodeData(coinbase));
                }

                JToken seq = jsonIn.GetValue("sequence");
                if(seq != null)
                {
                    txin.Sequence = (uint)seq;
                }
            }

            var vout = (JArray)json.GetValue("out");
            int voutCount = (int)json.GetValue("vout_sz");
            for(int i = 0; i < voutCount; i++)
            {
                var jsonOut = (JObject)vout[i];
                var txout = new TxOut();
                tx.Outputs.Add(txout);

                txout.Value = Money.Parse((string)jsonOut.GetValue("value"));
                txout.ScriptPubKey = new Script((string)jsonOut.GetValue("scriptPubKey"));
            }
        }

        protected override void WriteTransaction(JsonTextWriter writer, Transaction tx)
        {
            WritePropertyValue(writer, "hash", tx.GetHash().ToString());
            WritePropertyValue(writer, "ver", tx.Version);

            WritePropertyValue(writer, "vin_sz", tx.Inputs.Count);
            WritePropertyValue(writer, "vout_sz", tx.Outputs.Count);

            WritePropertyValue(writer, "lock_time", tx.LockTime.Value);

            WritePropertyValue(writer, "size", tx.GetSerializedSize());

            writer.WritePropertyName("in");
            writer.WriteStartArray();
            foreach(IndexedTxIn input in tx.Inputs.AsIndexedInputs())
            {
                TxIn txin = input.TxIn;
                writer.WriteStartObject();
                writer.WritePropertyName("prev_out");
                writer.WriteStartObject();
                WritePropertyValue(writer, "hash", txin.PrevOut.Hash.ToString());
                WritePropertyValue(writer, "n", txin.PrevOut.N);
                writer.WriteEndObject();

                if(txin.PrevOut.Hash == uint256.Zero)
                {
                    WritePropertyValue(writer, "coinbase", Encoders.Hex.EncodeData(txin.ScriptSig.ToBytes()));
                }
                else
                {
                    WritePropertyValue(writer, "scriptSig", txin.ScriptSig.ToString());
                }
                if(input.WitScript != WitScript.Empty)
                {
                    WritePropertyValue(writer, "witness", input.WitScript.ToString());
                }
                if(txin.Sequence != uint.MaxValue)
                {
                    WritePropertyValue(writer, "sequence", (uint)txin.Sequence);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("out");
            writer.WriteStartArray();

            foreach(TxOut txout in tx.Outputs)
            {
                writer.WriteStartObject();
                WritePropertyValue(writer, "value", txout.Value.ToString(false, false));
                WritePropertyValue(writer, "scriptPubKey", txout.ScriptPubKey.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }


    }
}
