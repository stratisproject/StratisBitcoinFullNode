using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC.Converters
{

    public class BtcDecimalJsonConverter : JsonConverter
    {
        const int _minDecimals = 8;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var d = (decimal)value;
            var rounded = Math.Round(d, _minDecimals);
            string result = d.ToString(CultureInfo.InvariantCulture);
            if (_minDecimals > 0)
            {
                if (!result.Contains('.') || result.Split('.')[1].Length < _minDecimals)
                {
                    result = d.ToString("0." + new string('0', _minDecimals), CultureInfo.InvariantCulture);
                }
            }
            writer.WriteRawValue(result);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal);
        }
    }
}
