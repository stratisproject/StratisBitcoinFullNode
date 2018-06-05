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
        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; set; }

        /// <summary>Minimum transaction fee for network.</summary>
        public FeeRate MinTxFeeRate { get; set; }

        /// <summary>Fall back transaction fee for network.</summary>
        public FeeRate FallbackTxFeeRate { get; set; }

        /// <summary>Minimum relay transaction fee for network.</summary>
        public FeeRate MinRelayTxFeeRate { get; set; }

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

            // TODO: Move to ConsensusSettings
            this.MaxTipAge = config.GetOrDefault("maxtipage", nodeSettings.Network.MaxTipAge);
            logger.LogDebug("MaxTipAge set to {0}.", this.MaxTipAge);

            // TODO: Move to WalletSettings
            this.MinTxFeeRate = new FeeRate(config.GetOrDefault("mintxfee", nodeSettings.Network.MinTxFee));
            logger.LogDebug("MinTxFeeRate set to {0}.", this.MinTxFeeRate);

            // TODO: Move to WalletSettings
            this.FallbackTxFeeRate = new FeeRate(config.GetOrDefault("fallbackfee", nodeSettings.Network.FallbackFee));
            logger.LogDebug("FallbackTxFeeRate set to {0}.", this.FallbackTxFeeRate);

            // TODO: Move to MempoolSettings
            this.MinRelayTxFeeRate = new FeeRate(config.GetOrDefault("minrelaytxfee", nodeSettings.Network.MinRelayTxFee));
            logger.LogDebug("MinRelayTxFeeRate set to {0}.", this.MinRelayTxFeeRate);

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
            builder.AppendLine($"-maxtipage=<number>       Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"-mintxfee=<number>        Minimum fee rate. Defaults to network specific value.");
            builder.AppendLine($"-fallbackfee=<number>     Fallback fee rate. Defaults to network specific value.");
            builder.AppendLine($"-minrelaytxfee=<number>   Minimum relay fee rate. Defaults to network specific value.");

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
            builder.AppendLine($"#Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"#maxtipage={network.MaxTipAge}");
            builder.AppendLine($"#Minimum fee rate. Defaults to {network.MinTxFee}.");
            builder.AppendLine($"#mintxfee={network.MinTxFee}");
            builder.AppendLine($"#Fallback fee rate. Defaults to {network.FallbackFee}.");
            builder.AppendLine($"#fallbackfee={network.FallbackFee}");
            builder.AppendLine($"#Minimum relay fee rate. Defaults to {network.MinRelayTxFee}.");
            builder.AppendLine($"#minrelaytxfee={network.MinRelayTxFee}");
        }
    }
}
