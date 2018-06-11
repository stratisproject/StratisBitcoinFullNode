﻿#if !NOJSONNET
using System;
using System.Reflection;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
#if !NOJSONNET
    public
#else
    internal
#endif
    class BitcoinStringJsonConverter : JsonConverter
    {
        public BitcoinStringJsonConverter()
        {

        }
        public BitcoinStringJsonConverter(Network network)
        {
            this.Network = network;
        }
        public override bool CanConvert(Type objectType)
        {
            return
                typeof(IBitcoinString).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) ||
                (typeof(IDestination).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) && objectType.GetTypeInfo().AssemblyQualifiedName.Contains("NBitcoin"));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;

            try
            {
                IBitcoinString result = Network.Parse(reader.Value.ToString(), null);
                if(result == null)
                {
                    throw new JsonObjectException("Invalid BitcoinString data", reader);
                }
                if(this.Network != null)
                {
                    if(result.Network != this.Network)
                    {
                        result = this.Network.Parse(reader.Value.ToString());
                        if(result.Network != this.Network)
                        {
                            throw new JsonObjectException("Invalid BitcoinString network", reader);
                        }
                    }
                }
                if(!objectType.GetTypeInfo().IsAssignableFrom(result.GetType().GetTypeInfo()))
                {
                    throw new JsonObjectException("Invalid BitcoinString type expected " + objectType.Name + ", actual " + result.GetType().Name, reader);
                }
                return result;
            }
            catch(FormatException)
            {
                throw new JsonObjectException("Invalid Base58Check data", reader);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var base58 = value as IBitcoinString;
            if(base58 != null)
            {
                writer.WriteValue(value.ToString());
            }
        }

        public Network Network
        {
            get;
            set;
        }
    }
}
#endif