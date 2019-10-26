using System;
using System.Reflection;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="KeyPath"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class KeyPathJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(KeyPath).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return reader.TokenType == JsonToken.Null ? null : KeyPath.Parse(reader.Value.ToString());
            }
            catch (FormatException)
            {
                throw new JsonObjectException("Invalid key path", reader);
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var keyPath = value as KeyPath;
            if (keyPath != null)
                writer.WriteValue(keyPath.ToString());
        }
    }
}
