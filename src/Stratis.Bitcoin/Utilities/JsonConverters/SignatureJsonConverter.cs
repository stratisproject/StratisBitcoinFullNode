using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="Script"/> or a <see cref="WitScript"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class SignatureJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ECDSASignature) || objectType == typeof(TransactionSignature);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            try
            {
                if (objectType == typeof(ECDSASignature))
                    return new ECDSASignature(Encoders.Hex.DecodeData((string)reader.Value));

                if (objectType == typeof(TransactionSignature))
                    return new TransactionSignature(Encoders.Hex.DecodeData((string)reader.Value));
            }
            catch
            {
            }

            throw new JsonObjectException("Invalid signature", reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value != null)
            {
                if (value is ECDSASignature)
                    writer.WriteValue(Encoders.Hex.EncodeData(((ECDSASignature)value).ToDER()));
                if (value is TransactionSignature)
                    writer.WriteValue(Encoders.Hex.EncodeData(((TransactionSignature)value).ToBytes()));
            }
        }
    }
}
