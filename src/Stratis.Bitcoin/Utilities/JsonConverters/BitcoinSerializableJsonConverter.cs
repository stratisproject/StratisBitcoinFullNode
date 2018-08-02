using System;
using System.IO;
using System.Reflection;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert an object implementing <see cref="IBitcoinSerializable"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public sealed class BitcoinSerializableJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                var obj = (IBitcoinSerializable)Activator.CreateInstance(objectType);
                byte[] bytes = Encoders.Hex.DecodeData((string)reader.Value);
                obj.ReadWrite(bytes);
                return obj;
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
            byte[] bytes = ((IBitcoinSerializable)value).ToBytes();
            writer.WriteValue(Encoders.Hex.EncodeData(bytes));
        }
    }
}