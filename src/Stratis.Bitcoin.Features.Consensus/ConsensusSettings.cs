using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Configurable settings for the consensus feature.
    /// </summary>
    public class ConsensusSettings
    {
        /// <summary>Whether use of checkpoints is enabled or not.</summary>
        public bool UseCheckpoints { get; set; }

        /// <summary>
        /// If this block is in the chain assume that it and its ancestors are valid and skip their script verification.
        /// Null to not assume valid blocks and therefore validate all blocks.
        /// </summary>
        public uint256 BlockAssumedValid { get; set; }

        /// <summary>
        /// Constructs a new consensus settings object.
        /// </summary>
        public ConsensusSettings()
        {
        }

        /// <summary>
        /// Load the consensus settings from the config settings.
        /// </summary>
        /// <param name="nodeSettings">The node's settings.</param>
        /// <returns>These consensus config settings.</returns>
        public ConsensusSettings Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            
            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.UseCheckpoints = config.GetOrDefault<bool>("checkpoints", true);

            if (config.GetAll("assumevalid").Any(i => i.Equals("0"))) // 0 means validate all blocks.
            {
                this.BlockAssumedValid = null;
            }
            else
            {
                this.BlockAssumedValid = config.GetOrDefault<uint256>("assumevalid", nodeSettings.Network.Consensus.DefaultAssumeValid);
            }

            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ConsensusSettings).FullName);
            logger.LogDebug("Checkpoints are {0}.", this.UseCheckpoints ? "enabled" : "disabled");
            logger.LogDebug("Assume valid block is '{0}'.", this.BlockAssumedValid == null ? "disabled" : this.BlockAssumedValid.ToString());

            return this;
        }

        /// <summary>Prints the help information on how to configure the Consensus settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            Guard.NotNull(network, nameof(network));

            var builder = new StringBuilder();

            builder.AppendLine($"-checkpoints=<0 or 1>     Use checkpoints. Default 1.");
            builder.AppendLine($"-assumevalid=<hex>        If this block is in the chain assume that it and its ancestors are valid and potentially skip their script verification (0 to verify all). Defaults to { network.Consensus.DefaultAssumeValid }.");

            NodeSettings.Default().Logger.LogInformation(builder.ToString());
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
        }
    }
}
