using System;
using System.IO;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
    public class KeyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Key) == objectType || typeof(PubKey) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;

            try
            {

                byte[] bytes = Encoders.Hex.DecodeData((string)reader.Value);
                if(objectType == typeof(Key))
                    return new Key(bytes);
                else
                    return new PubKey(bytes);
            }
            catch(EndOfStreamException)
            {
            }
            catch(FormatException)
            {
            }
            throw new JsonObjectException("Invalid bitcoin object of type " + objectType.Name, reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value != null)
            {
                byte[] bytes = ((IBitcoinSerializable)value).ToBytes();
                writer.WriteValue(Encoders.Hex.EncodeData(bytes));
            }
        }
    }
}