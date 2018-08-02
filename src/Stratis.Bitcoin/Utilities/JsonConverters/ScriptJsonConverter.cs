using System;
using System.Reflection;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="Script"/> or a <see cref="WitScript"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class ScriptJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(Script).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) || typeof(WitScript).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            try
            {
                if (objectType == typeof(Script))
                    return Script.FromBytesUnsafe(Encoders.Hex.DecodeData((string)reader.Value));
                if (objectType == typeof(WitScript))
                    return new WitScript((string)reader.Value);
            }
            catch (FormatException)
            {
            }

            throw new JsonObjectException("A script should be a byte string", reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                if (value is Script)
                    writer.WriteValue(Encoders.Hex.EncodeData(((Script)value).ToBytes(false)));
                if (value is WitScript)
                    writer.WriteValue(((WitScript)value).ToString());
            }
        }
    }
}