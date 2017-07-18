using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC.Converters
{
    public class BtcDecimalJsonConverter : JsonConverter
    {
        private const int MinDecimals = 8;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var d = (decimal)value;
            var result = d.ToString(CultureInfo.InvariantCulture);
            if (!result.Contains('.') || result.Split('.')[1].Length < MinDecimals)
            {
                result = d.ToString("0." + new string('0', MinDecimals), CultureInfo.InvariantCulture);
            }
            writer.WriteRawValue(result);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(decimal);
    }
}
