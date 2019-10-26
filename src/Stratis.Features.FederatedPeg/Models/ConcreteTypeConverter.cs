using System;
using Newtonsoft.Json;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.Models
{
    public class ConcreteConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        [NoTrace]
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<T>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
