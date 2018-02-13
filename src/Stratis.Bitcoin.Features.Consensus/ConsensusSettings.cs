using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

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
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ConsensusSettings).FullName);

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

            logger.LogDebug("Checkpoints are {0}.", this.UseCheckpoints ? "enabled" : "disabled");
            logger.LogDebug("Assume valid block is '{0}'.", this.BlockAssumedValid == null ? "disabled" : this.BlockAssumedValid.ToString());

            return this;
        }
    }
}
