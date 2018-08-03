using System;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="byte[]"/> to and from hex-encoded JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
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
            if (value != null)
            {
                writer.WriteValue(Encoders.Hex.EncodeData((byte[])value));
            }
        }
    }
}
