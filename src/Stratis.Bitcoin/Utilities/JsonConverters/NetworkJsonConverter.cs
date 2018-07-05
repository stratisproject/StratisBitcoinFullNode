using System;
using System.Reflection;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Utilities.JsonConverters
{
    /// <summary>
    /// Converter used to convert the name of a network in JSON to the corresponding <see cref="Network"/>.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
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
                return Network.Main;

            if (network.Equals("TestNet", StringComparison.OrdinalIgnoreCase) || network.Equals("test", StringComparison.OrdinalIgnoreCase))
                return Network.TestNet;

            if (network.Equals("RegTest", StringComparison.OrdinalIgnoreCase) || network.Equals("reg", StringComparison.OrdinalIgnoreCase))
                return Network.RegTest;

            Network net = NetworksContainer.GetNetwork(network);
            if(net != null)
                return net;

            throw new JsonObjectException("Unknown network (valid values : main, test, reg)", reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var network = (Network)value;

            string str = null;

            if(network == Network.Main)
                str = "MainNet";
            else if(network == Network.TestNet)
                str = "TestNet";
            else if(network == Network.RegTest)
                str = "RegTest";
            else if(network != null)
                str = network.ToString();

            if (str != null)
                writer.WriteValue(str);
        }
    }
}