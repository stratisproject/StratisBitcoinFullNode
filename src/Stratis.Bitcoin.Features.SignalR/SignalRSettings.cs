using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR
{
    /// <summary>
    /// Configuration related to the API interface.
    /// </summary>
    public class SignalRSettings
    {
        /// <summary>The default host used by the signalR when the node runs on the Stratis network.</summary>
        public const string DefaultSignalRHost = "http://localhost";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>URI to node's signal r interface.</summary>
        public Uri SignalRUri { get; set; }

        /// <summary>Port of node's Signal interface.</summary>
        public int SignalPort { get; set; }

        public SignalRSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(SignalRSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            string host = config.GetOrDefault("signalruri", DefaultSignalRHost, this.logger);
            var uri = new Uri(host);

            // Find out which port should be used for the API.
            int port = config.GetOrDefault("signalrport", nodeSettings.Network.DefaultSignalRPort, this.logger);

            // If no port is set in the API URI.
            if (uri.IsDefaultPort)
            {
                this.SignalRUri = new Uri($"{host}:{port}");
                this.SignalPort = port;
            }
            else
            {
                this.SignalRUri = uri;
                this.SignalPort = uri.Port;
            }
        }

        /// <summary>Prints the help information on how to configure the API settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-signalruri=<string>                  URI to node's SignalR interface. Defaults to '{DefaultSignalRHost}'.");
            builder.AppendLine($"-signalrport=<0-65535>                Port of node's SignalR interface. Defaults to {network.DefaultAPIPort}.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####API Settings####");
            builder.AppendLine($"#URI to node's API interface. Defaults to '{ DefaultSignalRHost }'.");
            builder.AppendLine($"#signalruri={ DefaultSignalRHost }");
            builder.AppendLine($"#Port of node's API interface. Defaults to { network.DefaultAPIPort }.");
            builder.AppendLine($"#signalrport={ network.DefaultAPIPort }");
        }
    }
}
