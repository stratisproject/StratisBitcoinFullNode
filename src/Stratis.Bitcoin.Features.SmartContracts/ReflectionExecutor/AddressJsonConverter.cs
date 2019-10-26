using System;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public class AddressJsonConverter : JsonConverter
    {
        private readonly Network network;

        public AddressJsonConverter(Network network)
        {
            this.network = network;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Address);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken t = JToken.FromObject(value);

            if (t.Type != JTokenType.Object)
            {
                t.WriteTo(writer);
            }
            else
            {
                JValue v = JValue.CreateString(((Address)value).ToUint160().ToBase58Address(this.network));
                v.WriteTo(writer);
            }
        }
    }
}
