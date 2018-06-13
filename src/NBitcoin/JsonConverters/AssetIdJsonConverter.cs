﻿#if !NOJSONNET
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
    class AssetIdJsonConverter : JsonConverter
    {
        public AssetIdJsonConverter(Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            this.Network = network;
        }
        public override bool CanConvert(Type objectType)
        {
            return typeof(AssetId).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                string value = reader.Value.ToString();
                return new BitcoinAssetId(value, this.Network).AssetId;
            }
            catch(FormatException)
            {
                throw new JsonObjectException("Invalid BitcoinAssetId ", reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var assetId = value as AssetId;
            if(assetId != null)
            {
                writer.WriteValue(assetId.ToString(this.Network));
            }
        }

        public Network Network
        {
            get;
            set;
        }
    }
}
#endif