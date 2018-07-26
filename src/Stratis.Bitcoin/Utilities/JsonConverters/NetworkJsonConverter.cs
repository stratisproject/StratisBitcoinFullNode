using System;
using System.Reflection;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert the name of a network in JSON to the corresponding <see cref="Network"/>.
    /// </summary>
    /// <seealso cref="JsonConverter" />
    public class NetworkJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(Network).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            string network = (string)reader.Value;

            if (network == null)
                return null;

            if (network.Equals("MainNet", StringComparison.OrdinalIgnoreCase) || network.Equals("main", StringComparison.OrdinalIgnoreCase))
                return NetworkContainer.Main;

            if (network.Equals("TestNet", StringComparison.OrdinalIgnoreCase) || network.Equals("test", StringComparison.OrdinalIgnoreCase))
                return NetworkContainer.TestNet;

            if (network.Equals("RegTest", StringComparison.OrdinalIgnoreCase) || network.Equals("reg", StringComparison.OrdinalIgnoreCase))
                return NetworkContainer.RegTest;

            Network net = NetworkContainer.GetNetwork(network);
            if(net != null)
                return net;

            throw new JsonObjectException("Unknown network (valid values : main, test, reg)", reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var network = (Network)value;

            string str = null;

            if(network == NetworkContainer.Main)
                str = "MainNet";
            else if(network == NetworkContainer.TestNet)
                str = "TestNet";
            else if(network == NetworkContainer.RegTest)
                str = "RegTest";
            else if(network != null)
                str = network.ToString();

            if (str != null)
                writer.WriteValue(str);
        }
    }
}