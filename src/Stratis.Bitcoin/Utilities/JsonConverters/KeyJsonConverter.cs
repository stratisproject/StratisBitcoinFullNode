using System;
using System.IO;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="Key"/> or a <see cref="PubKey"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class KeyJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(Key) == objectType || typeof(PubKey) == objectType;
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                byte[] bytes = Encoders.Hex.DecodeData((string)reader.Value);
                if (objectType == typeof(Key))
                    return new Key(bytes);
                else
                    return new PubKey(bytes);
            }
            catch (EndOfStreamException)
            {
            }
            catch (FormatException)
            {
            }

            throw new JsonObjectException("Invalid bitcoin object of type " + objectType.Name, reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                byte[] bytes = ((IBitcoinSerializable)value).ToBytes();
                writer.WriteValue(Encoders.Hex.EncodeData(bytes));
            }
        }
    }
}