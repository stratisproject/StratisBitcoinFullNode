#if !NOJSONNET
using System;
using System.Reflection;
using NBitcoin.OpenAsset;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
#if !NOJSONNET
    public
#else
    internal
#endif
    class CoinJsonConverter : JsonConverter
    {
        public class CoinJson
        {
            public CoinJson()
            {

            }
            public CoinJson(ICoin coin, Network network)
            {
                if (network == null)
                    network = Network.Main;
                this.TransactionId = coin.Outpoint.Hash;
                this.Index = coin.Outpoint.N;
                this.ScriptPubKey = coin.TxOut.ScriptPubKey;
                if (coin is ScriptCoin)
                {
                    this.RedeemScript = ((ScriptCoin)coin).Redeem;
                }
                if(coin is Coin)
                {
                    this.Value = ((Coin)coin).Amount;
                }
                if (coin is ColoredCoin)
                {
                    var cc = (ColoredCoin)coin;
                    this.AssetId = cc.AssetId.GetWif(network);
                    this.Quantity = cc.Amount.Quantity;
                    this.Value = cc.Bearer.Amount;
                    var scc = cc.Bearer as ScriptCoin;
                    if (scc != null)
                    {
                        this.RedeemScript = scc.Redeem;
                    }
                }
            }
            public ICoin ToCoin()
            {
                Coin coin = this.RedeemScript == null ? new Coin(new OutPoint(this.TransactionId, this.Index), new TxOut(this.Value, this.ScriptPubKey)) : new ScriptCoin(new OutPoint(this.TransactionId, this.Index), new TxOut(this.Value, this.ScriptPubKey), this.RedeemScript);
                if (this.AssetId != null)
                    return coin.ToColoredCoin(new AssetMoney(this.AssetId, this.Quantity));
                return coin;
            }

            public uint256 TransactionId
            {
                get;
                set;
            }
            public uint Index
            {
                get;
                set;
            }
            public Money Value
            {
                get;
                set;
            }

            public Script ScriptPubKey
            {
                get;
                set;
            }

            public Script RedeemScript
            {
                get;
                set;
            }
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public BitcoinAssetId AssetId
            {
                get;
                set;
            }
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public long Quantity
            {
                get;
                set;
            }
        }

        public CoinJsonConverter(Network network)
        {
            this.Network = network;
        }

        public Network Network
        {
            get;
            set;
        }
        public override bool CanConvert(Type objectType)
        {
            return typeof(ICoin).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return reader.TokenType == JsonToken.Null ? null : serializer.Deserialize<CoinJson>(reader).ToCoin();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new CoinJson((ICoin)value, this.Network));
        }
    }
}
#endif