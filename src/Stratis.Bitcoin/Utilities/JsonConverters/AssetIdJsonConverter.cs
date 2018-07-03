using System;
using System.Reflection;
using NBitcoin;
using NBitcoin.OpenAsset;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    public class AssetIdJsonConverter : JsonConverter
    {
        public Network Network { get; set; }

        public AssetIdJsonConverter(Network network)
        {
            Guard.NotNull(network, nameof(network));

            this.Network = network;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(AssetId).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                var value = reader.Value.ToString();
                return new BitcoinAssetId(value, this.Network).AssetId;
            }
            catch(FormatException)
            {
                throw new JsonObjectException("Invalid BitcoinAssetId ", reader);
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var assetId = value as AssetId;
            if(assetId != null)
            {
                writer.WriteValue(assetId.ToString(this.Network));
            }
        }
    }
}