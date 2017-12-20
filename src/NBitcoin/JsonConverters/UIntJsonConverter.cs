﻿#if !NOJSONNET
using System;
using System.IO;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
#if !NOJSONNET
    public
#else
    internal
#endif
    class UInt160JsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(uint160) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;
            try
            {
                return uint160.Parse((string)reader.Value);
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
            writer.WriteValue(value.ToString());
        }
    }
#if !NOJSONNET
    public
#else
    internal
#endif
    class UInt256JsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(uint256) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Null)
                return null;
            try
            {
                return uint256.Parse((string)reader.Value);
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
            writer.WriteValue(value.ToString());
        }
    }
}
#endif