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

        /// <summary><c>true</c> to maintain a full transaction index.</summary>
        public bool TxIndex { get; set; }

        /// <summary><c>true</c> to rebuild chain state and block index from block data files on disk.</summary>
        public bool ReIndex { get; set; }

        /// <summary>Amount of blocks that we should keep in case node is running in pruned mode.</summary>
        /// <remarks>Should only be used if <see cref="PruningEnabled"/> is <c>true</c>.</remarks>
        public int AmountOfBlocksToKeep { get; set; }

        public bool PruningEnabled { get; set; }

        /// <summary>The maximum size of bytes the cache can contain.</summary>
        public int MaxCacheSize { get; set; }

        /// <summary>Calculates minimum amount of blocks we need to keep during pruning.</summary>
        private int GetMinPruningAmount()
        {
            // We want to keep 48 hours worth of blocks. This is what BTC does.
            // To calculate this value we need to divide 48 hours by the target spacing.
            // We have no access to target spacing here before it's moved to network so fix it later.
            // TODO usae target spacing instead of hardcoded value.

            // TODO pick highest value between max reorg and amount of blocks it takes to fill 48 hours.

            return 2880;
        }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public StoreSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(StoreSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.AmountOfBlocksToKeep = config.GetOrDefault<int>("prune", 0, this.logger);
            this.PruningEnabled = this.AmountOfBlocksToKeep != 0;

            if (this.PruningEnabled && this.AmountOfBlocksToKeep < this.GetMinPruningAmount())
                throw new ConfigurationException($"The minimum amount of blocks to keep can't be less than {this.GetMinPruningAmount()}.");

            this.TxIndex = config.GetOrDefault<bool>("txindex", false, this.logger);
            this.ReIndex = config.GetOrDefault<bool>("reindex", false, this.logger);

            // For now we reuse the same value as ConsensusSetting, when store moves to core this can be updated.
            this.MaxCacheSize = config.GetOrDefault("maxblkstoremem", 5, this.logger);

            if (this.PruningEnabled && this.TxIndex)
                throw new ConfigurationException("Prune mode is incompatible with -txindex");
        }

        /// <summary>Prints the help information on how to configure the block store settings to the logger.</summary>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-txindex=<0 or 1>              Enable to maintain a full transaction index.");
            builder.AppendLine($"-reindex=<0 or 1>              Rebuild chain state and block index from block data files on disk.");
            builder.AppendLine($"-prune=<amount of blocks>      Enable pruning to reduce storage requirements by enabling deleting of old blocks. Value of 0 means pruning is disabled.");
            builder.AppendLine($"-maxblkstoremem=<number>       Max memory to use before flushing blocks to disk in MB. Default is 5 MB.");



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
            builder.AppendLine($"#prune=2880");
            builder.AppendLine($"#The maximum amount of blocks the cache can contain. Default is 5 MB");
            builder.AppendLine($"#maxblkstoremem=5");
        }
    }
}