using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusManager : IBlockDownloadState, INetworkDifficulty, IGetUnspentTransaction
    {
        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        public ConsensusLoop ConsensusLoop { get; private set; }
        public IDateTimeProvider DateTimeProvider { get; private set; }
        public NodeSettings NodeSettings { get; private set; }
        public Network Network { get; private set; }
        public PowConsensusValidator ConsensusValidator { get; private set; }
        public ChainState ChainState { get; private set; }

        public ConsensusManager(ICheckpoints checkpoints, ConsensusLoop consensusLoop = null, IDateTimeProvider dateTimeProvider = null, NodeSettings nodeSettings = null, Network network = null,
            PowConsensusValidator consensusValidator = null, ChainState chainState = null)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.NodeSettings = nodeSettings;
            this.Network = network;
            this.ConsensusValidator = consensusValidator;
            this.ChainState = chainState;
            this.checkpoints = checkpoints;
        }

        /// <summary>
        /// Checks whether the node is currently in the process of initial block download.
        /// </summary>
        /// <returns><c>true</c> if the node is currently doing IBD, <c>false</c> otherwise.</returns>
        public bool IsInitialBlockDownload()
        {
            if (this.ConsensusLoop == null)
                return false;

            if (this.ConsensusLoop.Tip == null)
                return true;

            if (this.checkpoints.GetLastCheckpointHeight() > this.ConsensusLoop.Tip.Height)
                return true;

            if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.NodeSettings.MaxTipAge))
                return true;

            return false;
        }

        public Target GetNetworkDifficulty()
        {
            if ((this.ConsensusValidator?.ConsensusParams != null) && (this.ChainState?.ConsensusTip != null))
                return this.ChainState?.ConsensusTip?.GetWorkRequired(this.ConsensusValidator.ConsensusParams);
            else
                return null;
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            CoinViews.FetchCoinsResponse response = null;
            if (this.ConsensusLoop?.UTXOSet != null)
                response = await this.ConsensusLoop.UTXOSet.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);
            return response?.UnspentOutputs?.SingleOrDefault();
        }
    }
}
