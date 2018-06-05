using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides IBD (Initial Block Download) state.
    /// </summary>
    /// <seealso cref="IInitialBlockDownloadState" />
    public class InitialBlockDownloadState : IInitialBlockDownloadState
    {
        /// <summary>A provider of the date and time.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>User defined node settings.</summary>
        private readonly BaseSettings baseSettings;

        /// <summary>
        /// Creates a new instance of the <see cref="InitialBlockDownloadState" /> class.
        /// </summary>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="baseSettings">User defined base settings.</param>
        /// <param name="checkpoints">Provider of block header hash checkpoints.</param>
        public InitialBlockDownloadState(IChainState chainState, Network network, BaseSettings baseSettings, ICheckpoints checkpoints)
        {
            this.network = network;
            this.baseSettings = baseSettings;
            this.chainState = chainState;
            this.checkpoints = checkpoints;
            this.dateTimeProvider = DateTimeProvider.Default;
        }

        /// <inheritdoc />
        public bool IsInitialBlockDownload()
        {
            if (this.chainState == null)
                return false;

            if (this.chainState.ConsensusTip == null)
                return true;

            if (this.checkpoints.GetLastCheckpointHeight() > this.chainState.ConsensusTip.Height)
                return true;

            if (this.chainState.ConsensusTip.ChainWork < (this.network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.chainState.ConsensusTip.Header.BlockTime.ToUnixTimeSeconds() < (this.dateTimeProvider.GetTime() - this.baseSettings.MaxTipAge))
                return true;

            return false;
        }
    }
}
