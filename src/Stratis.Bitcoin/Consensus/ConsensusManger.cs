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
    public class ConsensusManager
    {
        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly ConsensusSettings consensusSettings;
        private readonly ConcurrentChain concurrentChain;
        private readonly ILookaheadBlockPuller lookaheadBlockPuller;
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
            ILookaheadBlockPuller lookaheadBlockPuller,
            IConsensusRules consensusRules,
            IFinalizedBlockHeight finalizedBlockHeight)
        {
            this.network = network;
            this.chainState = chainState;
            this.consensusSettings = consensusSettings;
            this.concurrentChain = concurrentChain;
            this.lookaheadBlockPuller = lookaheadBlockPuller;
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, chainedHeaderValidator, checkpoints, chainState, finalizedBlockHeight, consensusSettings);

            this.blocksRequested = new Dictionary<uint256, List<Action>>();
            this.toDownloadQueue = new AsyncQueue<(ChainedHeader headerFrom, ChainedHeader headerTo)>();
        }

        public async Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            uint256 utxoHash = await this.consensusRules.GetBlockHashAsync().ConfigureAwait(false);
            bool blockStoreDisabled = this.chainState.BlockStoreTip == null;

            while (true)
            {
                this.Tip = this.concurrentChain.GetBlock(utxoHash);

                if ((this.Tip != null) && (blockStoreDisabled || (this.chainState.BlockStoreTip.Height >= this.Tip.Height)))
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                utxoHash = await this.consensusRules.RewindAsync().ConfigureAwait(false);
            }

            this.concurrentChain.SetTip(this.Tip);

            this.lookaheadBlockPuller.SetLocation(this.Tip);

            this.chainedHeaderTree.Initialize(this.Tip, !blockStoreDisabled);

            this.logger.LogTrace("(-)");
        }
    }
}
