using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Interface of block header hash checkpoint provider.
    /// </summary>
    public interface ICheckpoints
    {
        /// <summary>
        /// Obtains a height of the last checkpointed block.
        /// </summary>
        /// <returns>Height of the last checkpointed block, or 0 if no checkpoint is available.</returns>
        int GetLastCheckpointHeight();

        /// <summary>
        /// Checks if a block header hash at specific height is in violation with the hardcoded checkpoints.
        /// </summary>
        /// <param name="height">Height of the block.</param>
        /// <param name="hash">Block header hash to check.</param>
        /// <returns>
        /// <c>true</c> if either there is no checkpoint for the given height, or if the checkpointed block header hash equals
        /// to the checked <paramref name="hash"/>. <c>false</c> if there is a checkpoint for the given <paramref name="height"/>,
        /// but the checkpointed block header hash is not the same as the checked <paramref name="hash"/>.
        /// </returns>
        bool CheckHardened(int height, uint256 hash);

        /// <summary>
        /// Retrieves checkpoint for a block at given height.
        /// </summary>
        /// <param name="height">Height of the block.</param>
        /// <returns>Checkpoint information or <c>null</c> if a checkpoint does not exist for given <paramref name="height"/>.</returns>
        CheckpointInfo GetCheckpoint(int height);
    }

    /// <summary>
    /// Checkpoints is a mechanism on how to avoid validation of historic blocks for which there
    /// already is a consensus on the network. This allows speeding up IBD, especially on POS networks.
    /// </summary>
    /// <remarks>
    /// From https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L66 :
    /// What makes a good checkpoint block? It is surrounded by blocks with reasonable timestamps
    /// (no blocks before with a timestamp after, none after with timestamp before). It also contains
    /// no strange transactions.
    /// </remarks>
    public class Checkpoints : ICheckpoints
    {
        /// <summary>The current network. </summary>
        private readonly Network network;

        /// <summary>Consensus settings for the full node.</summary>
        private readonly ConsensusSettings consensusSettings;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public Checkpoints()
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet/stratis test/main.</param>
        /// <param name="consensusSettings">Consensus settings for node - used to see if checkpoints have been disabled or not.</param>
        public Checkpoints(Network network, ConsensusSettings consensusSettings)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));

            this.consensusSettings = consensusSettings;
            this.network = network;
        }

        /// <inheritdoc />
        [NoTrace]
        public int GetLastCheckpointHeight()
        {
            Dictionary<int, CheckpointInfo> checkpoints = this.GetCheckpoints();
            return checkpoints.Count > 0 ? checkpoints.Keys.Last() : 0;
        }

        /// <summary>
        /// Gets the last checkpoint.
        /// </summary>
        /// <returns>Last <see cref="CheckpointInfo"/> or null.</returns>
        [NoTrace]
        public CheckpointInfo GetLastCheckpoint(out int height)
        {
            var checkpoints = this.GetCheckpoints();
            if (checkpoints.Count == 0)
            {
                height = 0;
                return null;
            }
            else
            {
                height = checkpoints.Keys.Max();
                return checkpoints[height];
            }
        }

        /// <inheritdoc />
        [NoTrace]
        public bool CheckHardened(int height, uint256 hash)
        {
            CheckpointInfo checkpoint;
            if (!this.GetCheckpoints().TryGetValue(height, out checkpoint)) return true;

            return checkpoint.Hash.Equals(hash);
        }

        /// <inheritdoc />
        [NoTrace]
        public CheckpointInfo GetCheckpoint(int height)
        {
            CheckpointInfo checkpoint;
            this.GetCheckpoints().TryGetValue(height, out checkpoint);
            return checkpoint;
        }

        [NoTrace]
        private Dictionary<int, CheckpointInfo> GetCheckpoints()
        {
            if (this.consensusSettings == null || !this.consensusSettings.UseCheckpoints)
                return new Dictionary<int, CheckpointInfo>();

            return this.network.Checkpoints;
        }
    }
}