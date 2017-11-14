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
        /// <summary>Whether use of checkpoints is enabled or not.</summary>
        public bool UseCheckpoints { get; set; }

        /// <summary>
        /// If this block is in the chain assume that it and its ancestors are valid and skip their script verification. 
        /// Null to not assume valid blocks and therefore validate all blocks.
        /// </summary>
        public uint256 BlockAssumedValid { get; set; }

        /// <summary>
        /// Load the consensus settings from the config settings.
        /// </summary>
        /// <param name="config">The configuration settings.</param>
        /// <param name="network">The network for the settings.</param>
        public void Load(TextFileConfiguration config, Network network)
        {
            this.UseCheckpoints = config.GetOrDefault<bool>("checkpoints", true);

            this.BlockAssumedValid = config.GetOrDefault<uint256>("assumevalid", this.GetDefaultAssumeValidBlock(network));
            if (this.BlockAssumedValid == 0) // 0 means validate all blocks
                this.BlockAssumedValid = null;
        }

        /// <summary>
        /// Logs consensus settings to debug logger.
        /// </summary>
        /// <param name="logger">The current logger.</param>
        public void LogDebugSettings(ILogger logger)
        {
            logger.LogDebug("Checkpoints are {0}.", this.UseCheckpoints ? "enabled" : "disabled");
            logger.LogDebug("Assume valid block is '{0}'.", this.BlockAssumedValid == null ? "disabled" : this.BlockAssumedValid.ToString());
        }

        /// <summary>
        /// Gets the default setting for the hash for the block to assume is valid.
        /// </summary>
        /// <param name="network">Network to return hash for.</param>
        /// <returns>The hash for the assume valid block.</returns>
        private uint256 GetDefaultAssumeValidBlock(Network network)
        {
            // TODO: Figure out where to put defaults so they are easy to configure.
            uint256 defaultAssumeValidBlock = null;
            if (!network.IsBitcoin() && network.IsTest())
            {
                // Block Height 184096 https://chainz.cryptoid.info/strat-test/block.dws?184096.htm
                defaultAssumeValidBlock = new uint256("0x74427b2f85b5d9658ee81f7e73526441311122f2b23702b794be557ba43ca43e");
            }
            // TODO: Add defaults for bitcoin and mainnet networks.

            return defaultAssumeValidBlock;
        }
    }
}
