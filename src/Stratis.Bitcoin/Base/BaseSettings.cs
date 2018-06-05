using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class BaseSettings
    {
        const int MaximumAgentPrefixLength = 10;

        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; set; }

        /// <summary>Minimum transaction fee for network.</summary>
        public FeeRate MinTxFeeRate { get; set; }

        /// <summary>Fall back transaction fee for network.</summary>
        public FeeRate FallbackTxFeeRate { get; set; }

        /// <summary>Minimum relay transaction fee for network.</summary>
        public FeeRate MinRelayTxFeeRate { get; set; }

        /// <summary><c>true</c> to sync time with other peers and calculate adjusted time, <c>false</c> to use our system clock only.</summary>
        public bool SyncTimeEnabled { get; set; }

        private Action<BaseSettings> callback = null;

        public BaseSettings()
        {
        }

        public BaseSettings(Action<BaseSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        public BaseSettings(NodeSettings nodeSettings, Action<BaseSettings> callback = null)
            : this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the node settings from the application configuration.
        /// </summary>
        public BaseSettings Load(NodeSettings nodeSettings)
        {
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(BaseSettings).FullName);

            logger.LogTrace("()");

            var config = nodeSettings.ConfigReader;

            this.MaxTipAge = config.GetOrDefault("maxtipage", nodeSettings.Network.MaxTipAge);
            logger.LogDebug("MaxTipAge set to {0}.", this.MaxTipAge);

            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", nodeSettings.Network.MinTxFee));
            logger.LogDebug("MinTxFeeRate set to {0}.", this.MinTxFeeRate);

            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", nodeSettings.Network.FallbackFee));
            logger.LogDebug("FallbackTxFeeRate set to {0}.", this.FallbackTxFeeRate);

            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", nodeSettings.Network.MinRelayTxFee));
            logger.LogDebug("MinRelayTxFeeRate set to {0}.", this.MinRelayTxFeeRate);

            this.SyncTimeEnabled = config.GetOrDefault<bool>("synctime", true);
            logger.LogDebug("Time synchronization with peers is {0}.", this.SyncTimeEnabled ? "enabled" : "disabled");

            var agentPrefix = config.GetOrDefault("agentprefix", string.Empty).Replace("-", "");

            if (agentPrefix.Length > MaximumAgentPrefixLength)
                agentPrefix = agentPrefix.Substring(0, MaximumAgentPrefixLength);
            logger.LogDebug("AgentPrefix set to {0}.", agentPrefix);

            // Since we are relying on the "Agent" value that may have been changed by an earlier call to 
            // this method follow good coding practice and ensure that we always get the same result on subsequent calls.
            var agent = nodeSettings.Agent.Substring(nodeSettings.Agent.IndexOf("-") + 1);
            nodeSettings.Agent = string.IsNullOrEmpty(agentPrefix) ? agent : $"{agentPrefix}-{agent}";
            logger.LogDebug("Agent set to {0}.", nodeSettings.Agent);

            logger.LogTrace("(-)");

            return this;
        }

        /// <summary>
        /// Displays command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            Guard.NotNull(network, nameof(network));

            var defaults = NodeSettings.Default(network: network);
            var daemonName = Path.GetFileName(Assembly.GetEntryAssembly().Location);

            var builder = new StringBuilder();
            builder.AppendLine("Usage:");
            builder.AppendLine($" dotnet run {daemonName} [arguments]");
            builder.AppendLine();
            builder.AppendLine("Command line arguments:");
            builder.AppendLine();
            builder.AppendLine($"-help/--help              Show this help.");
            builder.AppendLine($"-conf=<Path>              Path to the configuration file. Default {defaults.ConfigurationFile}.");
            builder.AppendLine($"-datadir=<Path>           Path to the data directory. Default {defaults.DataDir}.");
            builder.AppendLine($"-testnet                  Use the testnet chain.");
            builder.AppendLine($"-regtest                  Use the regtestnet chain.");
            builder.AppendLine($"-agentprefix=<string>     An optional prefix for the node's user agent that will be shared with peers in the version handshake.");
            builder.AppendLine($"-maxtipage=<number>       Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"-synctime=<0 or 1>        Sync with peers. Default 1.");
            builder.AppendLine($"-mintxfee=<number>        Minimum fee rate. Defaults to network specific value.");
            builder.AppendLine($"-fallbackfee=<number>     Fallback fee rate. Defaults to network specific value.");
            builder.AppendLine($"-minrelaytxfee=<number>   Minimum relay fee rate. Defaults to network specific value.");

            // Connection manager settings
            builder.AppendLine($"-port=<port>              The default network port to connect to. Default { network.DefaultPort }.");
            builder.AppendLine($"-connect=<ip:port>        Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"-addnode=<ip:port>        Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"-whitebind=<ip:port>      Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"-externalip=<ip>          Specify your own public address.");
            builder.AppendLine($"-bantime=<number>         Number of seconds to keep misbehaving peers from reconnecting. Default {ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds}.");
            builder.AppendLine($"-maxoutboundconnections=<number> The maximum number of outbound connections. Default {ConnectionManagerSettings.DefaultMaxOutboundConnections}.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Node Settings####");
            builder.AppendLine($"#An optional prefix for the node's user agent shared with peers. Truncated if over { MaximumAgentPrefixLength } characters.");
            builder.AppendLine($"#agentprefix=<string>");
            builder.AppendLine($"#Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"#maxtipage={network.MaxTipAge}");
            builder.AppendLine($"#Sync with peers. Default 1.");
            builder.AppendLine($"#synctime=1");
            builder.AppendLine($"#Minimum fee rate. Defaults to {network.MinTxFee}.");
            builder.AppendLine($"#mintxfee={network.MinTxFee}");
            builder.AppendLine($"#Fallback fee rate. Defaults to {network.FallbackFee}.");
            builder.AppendLine($"#fallbackfee={network.FallbackFee}");
            builder.AppendLine($"#Minimum relay fee rate. Defaults to {network.MinRelayTxFee}.");
            builder.AppendLine($"#minrelaytxfee={network.MinRelayTxFee}");
            builder.AppendLine();
            builder.AppendLine("####ConnectionManager Settings####");
            builder.AppendLine($"#The default network port to connect to. Default { network.DefaultPort }.");
            builder.AppendLine($"#port={network.DefaultPort}");
            builder.AppendLine($"#Specified node to connect to. Can be specified multiple times.");
            builder.AppendLine($"#connect=<ip:port>");
            builder.AppendLine($"#Add a node to connect to and attempt to keep the connection open. Can be specified multiple times.");
            builder.AppendLine($"#addnode=<ip:port>");
            builder.AppendLine($"#Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6. Can be specified multiple times.");
            builder.AppendLine($"#whitebind=<ip:port>");
            builder.AppendLine($"#Specify your own public address.");
            builder.AppendLine($"#externalip=<ip>");
            builder.AppendLine($"#Number of seconds to keep misbehaving peers from reconnecting. Default {ConnectionManagerSettings.DefaultMisbehavingBantimeSeconds}.");
            builder.AppendLine($"#bantime=<number>");
            builder.AppendLine($"#The maximum number of outbound connections. Default {ConnectionManagerSettings.DefaultMaxOutboundConnections}.");
            builder.AppendLine($"#maxoutboundconnections=<number>");
        }
    }
}
