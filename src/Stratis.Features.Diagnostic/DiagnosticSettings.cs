using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Diagnostic.PeerDiagnostic;

namespace Stratis.Features.Diagnostic
{
    /// <summary>
    /// Configuration related to the API interface.
    /// </summary>
    public class DiagnosticSettings
    {
        /// <summary>The default value for peers statistics collector.</summary>
        public const bool DefaultPeersStatisticsCollectorEnabled = false;
        
        /// <summary>The default value for maximum peers logged events.</summary>
        public const int DefaultMaxPeerLoggedEvents = 10;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="PeerStatisticsCollector"/> starts enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if <see cref="PeerStatisticsCollector"/> starts enabled; otherwise, <c>false</c>.
        /// </value>
        public bool PeersStatisticsCollectorEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum peer logged events in <see cref="PeerStatisticsCollector"/>.
        /// </summary>
        /// <value>
        /// The maximum peer logged events.
        /// </value>
        public int MaxPeerLoggedEvents { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public DiagnosticSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.PeersStatisticsCollectorEnabled = config.GetOrDefault("diagpeerstats", DefaultPeersStatisticsCollectorEnabled, this.logger);
            this.MaxPeerLoggedEvents = config.GetOrDefault("diagpeerstatsmaxlog", DefaultMaxPeerLoggedEvents, this.logger);
        }

        /// <summary>Prints the help information on how to configure the Diagnostic Feature to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"**DIAGNOSTIC SETTINGS**");
            builder.AppendLine($"-diagpeerstats=<bool>             Start the diagnostic peer statistics collector. Defaults to {DefaultPeersStatisticsCollectorEnabled}.");
            builder.AppendLine($"-diagpeerstatsmaxlog=<number>     Maximum number of logged peer events stored during the diagnostic peer statistics collector. Defaults to {DefaultMaxPeerLoggedEvents}.");
            builder.AppendLine($"**END OF DIAGNOSTIC SETTINGS**");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }
    }
}