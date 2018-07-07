using System;
using System.Reflection;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert a <see cref="DateTime"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class DateTimeToUnixTimeConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(DateTime).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) ||
                typeof(DateTimeOffset).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) ||
                typeof(DateTimeOffset?).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
                return null;
            DateTimeOffset result = Utils.UnixTimeToDateTime((ulong)(long)reader.Value);
            if (objectType == typeof(DateTime))
                return result.UtcDateTime;
            return result;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DateTime time;
            if (value is DateTime)
                time = (DateTime)value;
            else
                time = ((DateTimeOffset)value).UtcDateTime;

            if (time < Utils.UnixTimeToDateTime(0))
                time = Utils.UnixTimeToDateTime(0).UtcDateTime;
            writer.WriteValue(Utils.DateTimeToUnixTime(time));
        }
    }
}
