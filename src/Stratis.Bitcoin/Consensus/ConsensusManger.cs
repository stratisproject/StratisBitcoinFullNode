using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public interface IBlockPuller
    {

    }

    public class ConsensusManager
    {
        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly ConsensusSettings consensusSettings;
        private readonly IBlockPuller blockPuller;
        private readonly IConsensusRules consensusRules;

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader Tip { get; private set; }

        private Dictionary<uint256, List<Action>> blocksRequested;

        private AsyncQueue<(ChainedHeader headerFrom, ChainedHeader headerTo)> toDownloadQueue;

        public ConsensusManager(
            Network network, 
            ILoggerFactory loggerFactory, 
            IChainState chainState, 
            IChainedHeaderValidator chainedHeaderValidator, 
            ICheckpoints checkpoints, 
            ConsensusSettings consensusSettings, 
            ConcurrentChain concurrentChain,
            IBlockPuller blockPuller,
            IConsensusRules consensusRules,
            IFinalizedBlockHeight finalizedBlockHeight)
        {
            this.network = network;
            this.chainState = chainState;
            this.consensusSettings = consensusSettings;
            this.blockPuller = blockPuller;
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, chainedHeaderValidator, checkpoints, chainState, finalizedBlockHeight, consensusSettings);

            this.blocksRequested = new Dictionary<uint256, List<Action>>();
            this.toDownloadQueue = new AsyncQueue<(ChainedHeader headerFrom, ChainedHeader headerTo)>();
        }

        /// <summary>
        /// Set the tip of <see cref="ConsensusManager"/> to be the header that is stored to disk,
        /// if the given <see cref="consensusTip"/> is not in store then rewind store till a tip is found.
        /// If <see cref="IChainState.BlockStoreTip"/> is not null (block store is enabled) ensure the store and consensus manager are on the same tip,
        /// if consensus manager is ahead then reorg it to be the same height as store.
        /// </summary>
        /// <param name="consensusTip">The consensus tip that is attempted to be set.</param>
        public async Task InitializeAsync(ChainedHeader consensusTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(consensusTip), consensusTip);

            // TODO: consensus store
            // We should consider creating a consensus store class that will internally contain
            //  coinview and it will abstract the methods `RewindAsync()` `GetBlockHashAsync()` 

            uint256 utxoHash = await this.consensusRules.GetBlockHashAsync().ConfigureAwait(false);
            bool blockStoreDisabled = this.chainState.BlockStoreTip == null;

            while (true)
            {
                this.Tip = consensusTip.FindAncestorOrSelf(utxoHash);

                if ((this.Tip != null) && (blockStoreDisabled || (this.chainState.BlockStoreTip.Height >= this.Tip.Height)))
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                utxoHash = await this.consensusRules.RewindAsync().ConfigureAwait(false);
            }

            this.chainedHeaderTree.Initialize(this.Tip, !blockStoreDisabled);

            this.logger.LogTrace("(-)");
        }
    }
}
