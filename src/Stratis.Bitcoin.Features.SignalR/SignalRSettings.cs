using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRSettings
    {
        public const int DefaultSignalRPort = 31415;
        public const string DefaultSignalRHubRoute = "hub";

        public const string SignalRHubRouteParam = "signalrhubroute";
        public const string SignalRPortParam = "signalrport";

        private readonly ILogger logger;

        public string HubRoute { get; }

        public int Port { get; }

        public static SignalRSettings Defaults = new SignalRSettings();

        private SignalRSettings()
        {
            HubRoute = DefaultSignalRHubRoute;
            Port = DefaultSignalRPort;
        }

        public SignalRSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(SignalRSettings).FullName);
            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.HubRoute = config.GetOrDefault(SignalRHubRouteParam, DefaultSignalRHubRoute);
            this.Port = config.GetOrDefault(SignalRPortParam, DefaultSignalRPort);
        }

        /// <summary>Prints the help information on how to configure the SignalR settings to the logger.</summary>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-{SignalRHubRouteParam}=<string>                  URI to node's API interface. Defaults to '{DefaultSignalRPort}'.");
            builder.AppendLine($"-{SignalRPortParam}=<0-65535>                     Port of node's API interface. Defaults to {DefaultSignalRHubRoute}.");
            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####SignalR Settings####");
            builder.AppendLine($"#Route at which the SignalR hub will be publishing messages. Defaults to '{DefaultSignalRHubRoute}'.");
            builder.AppendLine($"#{SignalRHubRouteParam}={DefaultSignalRHubRoute}");
            builder.AppendLine($"#Port used by the node's signalR hub. Defaults to {DefaultSignalRPort}.");
            builder.AppendLine($"#{SignalRPortParam}={DefaultSignalRPort}");
        }
    }
}
