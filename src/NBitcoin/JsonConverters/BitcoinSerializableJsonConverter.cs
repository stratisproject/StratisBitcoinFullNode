﻿using System;
using System.IO;
using System.Reflection;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
    public class BitcoinSerializableJsonConverter : JsonConverter
    {
        private readonly Network network;

        public BitcoinSerializableJsonConverter()
        { }

        public BitcoinSerializableJsonConverter(Network network)
        {
            this.network = network;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IBitcoinSerializable).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            try
            {

                var obj = (IBitcoinSerializable)Activator.CreateInstance(objectType);
                byte[] bytes = Encoders.Hex.DecodeData((string)reader.Value);
                obj.ReadWrite(bytes, consensusFactory: this.network.Consensus.ConsensusFactory);
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

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            byte[] bytes = ((IBitcoinSerializable)value).ToBytes();
            writer.WriteValue(Encoders.Hex.EncodeData(bytes));
        }
    }
}