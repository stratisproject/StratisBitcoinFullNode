using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert <see cref="IPEndPoint"/> to and from JSON.
    /// </summary>
    /// <seealso cref="JsonConverter" />
    public sealed class IPEndPointConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IPEndPoint);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string json = JToken.Load(reader).ToString();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            string[] endPointComponents = json.Split('|');
            return new IPEndPoint(IPAddress.Parse(endPointComponents[0]), Convert.ToInt32(endPointComponents[1]));
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address != null || ipEndPoint.Port != 0)
                {
                    JToken.FromObject($"{ipEndPoint.Address}|{ipEndPoint.Port}").WriteTo(writer);
                    return;
                }
            }

            writer.WriteNull();
        }
    }
}