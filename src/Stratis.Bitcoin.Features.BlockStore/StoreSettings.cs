using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class StoreSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        // Initialize 'MaxCacheBlocksCount' with default value of maximum 300 blocks or with user defined value.
        // Value of 300 is chosen because it covers most of the cases when not synced node is connected and trying to sync from us.
        private const int DefaultMaxCacheBlocksCount = 300;

        /// <summary><c>true</c> to maintain a full transaction index.</summary>
        public bool TxIndex { get; set; }

        /// <summary><c>true</c> to rebuild chain state and block index from block data files on disk.</summary>
        public bool ReIndex { get; set; }

        /// <summary><c>true</c> to enable pruning to reduce storage requirements by enabling deleting of old blocks.</summary>
        public bool Prune { get; set; }

        /// <summary>The maximum amount of blocks the cache can contain.</summary>
        public int MaxCacheBlocksCount { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public StoreSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(StoreSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.Prune = config.GetOrDefault<bool>("prune", false, this.logger);
            this.TxIndex = config.GetOrDefault<bool>("txindex", false, this.logger);
            this.ReIndex = config.GetOrDefault<bool>("reindex", false, this.logger);
            this.MaxCacheBlocksCount = nodeSettings.ConfigReader.GetOrDefault("maxCacheBlocksCount", DefaultMaxCacheBlocksCount, this.logger);

            if (this.Prune && this.TxIndex)
                throw new ConfigurationException("Prune mode is incompatible with -txindex");
        }

        /// <summary>Prints the help information on how to configure the block store settings to the logger.</summary>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-txindex=<0 or 1>         Enable to maintain a full transaction index.");
            builder.AppendLine($"-reindex=<0 or 1>         Rebuild chain state and block index from block data files on disk.");
            builder.AppendLine($"-prune=<0 or 1>           Enable pruning to reduce storage requirements by enabling deleting of old blocks.");
            builder.AppendLine($"-maxCacheBlocksCount=<number> The maximum amount of blocks the cache can contain. Default is {DefaultMaxCacheBlocksCount}.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####BlockStore Settings####");
            builder.AppendLine($"#Enable to maintain a full transaction index.");
            builder.AppendLine($"#txindex=0");
            builder.AppendLine($"#Rebuild chain state and block index from block data files on disk.");
            builder.AppendLine($"#reindex=0");
            builder.AppendLine($"#Enable pruning to reduce storage requirements by enabling deleting of old blocks.");
            builder.AppendLine($"#prune=0");
            builder.AppendLine($"#The maximum amount of blocks the cache can contain. Default is {DefaultMaxCacheBlocksCount}");
            builder.AppendLine($"#maxCacheBlocksCount=300");
        }
    }
}