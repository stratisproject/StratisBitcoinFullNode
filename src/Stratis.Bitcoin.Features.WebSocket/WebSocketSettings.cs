using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketSettings
    {
        public const int DefaultPort = 4336;

        public const string WebSocketPortParam = "wsport";

        public static WebSocketSettings Defaults = new WebSocketSettings();

        /// <summary>Prints the help information on how to configure the SignalR settings to the logger.</summary>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"-{WebSocketPortParam}=<0-65535>                     Port of node's API interface. Defaults to {DefaultPort}.");
            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Web Socket Settings####");
            builder.AppendLine($"#Port used by the node's web socket hub. Defaults to {DefaultPort}.");
            builder.AppendLine($"#{WebSocketPortParam}={DefaultPort}");
        }
    }
}
