using System;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    public class HexJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(byte[]);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return reader.TokenType == JsonToken.Null ? null : Encoders.Hex.DecodeData((string)reader.Value);
            }
            catch
            {
                throw new JsonObjectException("Invalid hex", reader);
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value != null)
            {
                writer.WriteValue(Encoders.Hex.EncodeData((byte[])value));
            }
        }
    }
}
