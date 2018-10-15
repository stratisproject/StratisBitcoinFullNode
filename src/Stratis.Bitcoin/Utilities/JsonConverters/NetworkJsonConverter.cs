using System;
using System.Reflection;
using NBitcoin;
using NBitcoin.Networks;
using Newtonsoft.Json;

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

            string networkName = (string)reader.Value;

            if (networkName == null)
                return null;

            if (networkName.Equals("MainNet", StringComparison.OrdinalIgnoreCase) || networkName.Equals("main", StringComparison.OrdinalIgnoreCase))
                return NetworkRegistration.GetNetwork("MainNet");

            if (networkName.Equals("TestNet", StringComparison.OrdinalIgnoreCase) || networkName.Equals("test", StringComparison.OrdinalIgnoreCase))
                return NetworkRegistration.GetNetwork("TestNet");

            if (networkName.Equals("RegTest", StringComparison.OrdinalIgnoreCase) || networkName.Equals("reg", StringComparison.OrdinalIgnoreCase))
                return NetworkRegistration.GetNetwork("RegTest");

            Network network = NetworkRegistration.GetNetwork(networkName);
            if (network != null)
                return network;

            throw new JsonObjectException("Unknown network (valid values : main, test, reg)", reader);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var network = (Network)value;
            Guard.NotNull(network, nameof(network));

            if (string.IsNullOrEmpty(network.Name))
                throw new ArgumentException("Network name was not supplied.");

            writer.WriteValue(network.Name);
        }
    }
}