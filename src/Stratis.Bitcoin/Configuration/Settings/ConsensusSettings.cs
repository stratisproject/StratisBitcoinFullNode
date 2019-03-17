using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configurable settings for the consensus feature.
    /// </summary>
    public class ConsensusSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Whether use of checkpoints is enabled or not.</summary>
        public bool UseCheckpoints { get; set; }

        /// <summary>
        /// If this block is in the chain assume that it and its ancestors are valid and skip their script verification.
        /// Null to not assume valid blocks and therefore validate all blocks.
        /// </summary>
        public uint256 BlockAssumedValid { get; set; }

        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; private set; }

        /// <summary>
        /// Maximum memory to use for unconsume blocks in MB.
        /// Used in consensus manager to set <seealso cref="ConsensusManager.MaxUnconsumedBlocksDataBytes"/>
        /// </summary>
        public int MaxBlockMemoryInMB { get; private set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public ConsensusSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ConsensusSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.UseCheckpoints = config.GetOrDefault<bool>("checkpoints", true, this.logger);
            this.BlockAssumedValid = config.GetOrDefault<uint256>("assumevalid", nodeSettings.Network.Consensus.DefaultAssumeValid, this.logger);
            this.MaxTipAge = config.GetOrDefault("maxtipage", nodeSettings.Network.MaxTipAge, this.logger);
            this.MaxBlockMemoryInMB = config.GetOrDefault("maxblkmem", 200, this.logger);
        }

        /// <summary>Prints the help information on how to configure the Consensus settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            Guard.NotNull(network, nameof(network));

            var builder = new StringBuilder();

            builder.AppendLine($"-checkpoints=<0 or 1>     Use checkpoints. Default 1.");
            builder.AppendLine($"-assumevalid=<hex>        If this block is in the chain assume that it and its ancestors are valid and potentially skip their script verification (0 to verify all). Defaults to { network.Consensus.DefaultAssumeValid }.");
            builder.AppendLine($"-maxtipage=<number>       Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"-maxblkmem=<number>       Max memory to use for unconsumed blocks in MB. Default 200.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Consensus Settings####");
            builder.AppendLine($"#Use checkpoints. Default 1.");
            builder.AppendLine($"#checkpoints=1");
            builder.AppendLine($"#If this block is in the chain assume that it and its ancestors are valid and potentially skip their script verification (0 to verify all). Defaults to { network.Consensus.DefaultAssumeValid }.");
            builder.AppendLine($"#assumevalid={network.Consensus.DefaultAssumeValid}");
            builder.AppendLine($"#Max tip age. Default {network.MaxTipAge}.");
            builder.AppendLine($"#maxtipage={network.MaxTipAge}");
            builder.AppendLine($"#maxblkmem=200");
        }
    }
}
