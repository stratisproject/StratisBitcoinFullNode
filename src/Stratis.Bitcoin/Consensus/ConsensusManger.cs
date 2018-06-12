using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Consensus
{
    public interface IBlockPuller
    {
        void NewTipClaimed();

        long AverageBlockSize { get; }

        void RequestNewData(BlockDownloadRequest downloadRequest);

        void PeerDisconnected(int networkPeerId);
    }

    public class ConsensusManager
    {
        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly IChainedHeaderValidator chainedHeaderValidator;
        private readonly ConsensusSettings consensusSettings;
        private readonly IBlockPuller blockPuller;
        private readonly IConsensusRules consensusRules;
        private readonly IBlockStore blockStore;

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader Tip { get; private set; }

        private readonly Dictionary<uint256, ProcessDownloadedBlockDelegate> blocksRequested;

        private readonly Queue<BlockDownloadRequest> toDownloadQueue;

        private readonly object treeLock;

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
            IFinalizedBlockHeight finalizedBlockHeight,
            IBlockStore blockStore = null)
        {
            this.network = network;
            this.chainState = chainState;
            this.chainedHeaderValidator = chainedHeaderValidator;
            this.consensusSettings = consensusSettings;
            this.blockPuller = blockPuller;
            this.consensusRules = consensusRules;
            this.blockStore = blockStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, chainedHeaderValidator, checkpoints, chainState, finalizedBlockHeight, consensusSettings);

            this.treeLock = new object();

            this.blocksRequested = new Dictionary<uint256, ProcessDownloadedBlockDelegate>();
            this.toDownloadQueue = new Queue<BlockDownloadRequest>();
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

        public ChainedHeader HeadersPresentedAsync(int networkPeerId, List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(networkPeerId), networkPeerId, nameof(headers), nameof(headers.Count), headers.Count);

            ConnectNewHeadersResult newHeaders = null;

            try
            {
                lock (this.treeLock)
                {
                    newHeaders = this.chainedHeaderTree.ConnectNewHeaders(networkPeerId, headers);
                }
            }
            catch (ConsensusException cex)
            {
                 //TODO:
                switch (cex)
                {
                    case ConnectHeaderException exh:
                        break;

                    case InvalidHeaderException inex:
                        break;
                }
               
                throw;
            }

            if (newHeaders.DownloadTo != null)
            {
                this.DownloadBlocks(newHeaders.ToHashList(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-):'{0}'", newHeaders.Consumed);
            return newHeaders.Consumed;
        }

        public void PeerDisconnected(int peerId)
        {
            lock (this.treeLock)
            {
                this.chainedHeaderTree.PeerDisconnected(peerId);
                this.blockPuller.PeerDisconnected(peerId);
                this.ProcessDownloadQueueLocked();
            }
        }

        private void ProcessDownloadedBlock(BlockPair blockPair)
        {
            bool partialValidationRequired = false;

            lock (this.treeLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(blockPair.ChainedHeader, blockPair.Block);
            }

            if (partialValidationRequired)
            {
                this.chainedHeaderValidator.StartPartialValidation(blockPair, this.onPartialValidationCompletedCallback);
            }
        }

        private void onPartialValidationCompletedCallback(BlockPair blockPair, bool success)
        {
            // TODO
        }

        private void DownloadBlocks(List<uint256> blockHashes, ProcessDownloadedBlockDelegate processesBlock)
        {
            BlockDownloadRequest request = new BlockDownloadRequest
            {
                BlocksToDownload = blockHashes,
                DownloadedBlockDelegate = processesBlock
            };

            lock (this.treeLock)
            {
                this.toDownloadQueue.Enqueue(request);
                this.ProcessDownloadQueueLocked();
            }
        }

        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, ProcessDownloadedBlockDelegate processesBlock)
        {
            // TODO: verify the blockHashes are consecutive

            List<uint256> downloadJob = new List<uint256>();

            for (int i = blockHashes.Count - 1; i >= 0; i--)
            {
                uint256 blockHash = blockHashes[i];
                BlockPair blockPair = null;

                lock (this.treeLock)
                {
                    blockPair = this.chainedHeaderTree.GetBlockPair(blockHash);
                }

                if (blockPair == null)
                {
                    processesBlock(null);
                    continue;
                }

                if (blockPair.Block != null)
                {
                    processesBlock(blockPair);
                    continue;
                }

                if (this.blockStore != null)
                {
                    Block block = await this.blockStore.GetBlockAsync(blockHash);
                    if (block != null)
                    {
                        processesBlock(new BlockPair(block, blockPair.ChainedHeader));
                        continue;
                    }
                }

                downloadJob.Add(blockHash);
            }

            // Note in this case the list of headers might not be consecutive anymore.
            this.DownloadBlocks(downloadJob, this.ProcessDownloadedBlock);
        }

        private void ProcessDownloadQueueLocked()
        {
            const long MaxUnconsumedBlocksDataBytes = 1048576 * 10; // 10 mb

            while (this.toDownloadQueue.Count > 0)
            {
                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                long freeMb = MaxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes;

                if (freeMb <= 32)
                    return;

                long blocksToAsk = freeMb / this.blockPuller.AverageBlockSize;

                if (request.BlocksToDownload.Count < blocksToAsk)
                {
                    this.toDownloadQueue.Dequeue();
                    this.blockPuller.RequestNewData(request);
                }
                else
                {
                    // split queue item in 2 pieces - one if size blocksToAsk and second is rest.Ask BP for first part, leave 2nd part in queue

                    var newRequest = new BlockDownloadRequest()
                    {
                        DownloadedBlockDelegate = request.DownloadedBlockDelegate,
                        BlocksToDownload = new List<uint256>()
                    };

                    for (int i = 0; i < blocksToAsk; i++)
                    {
                        uint256 hash = request.BlocksToDownload[0];
                        request.BlocksToDownload.Remove(hash);
                        newRequest.BlocksToDownload.Add(hash);
                    }

                    this.blockPuller.RequestNewData(newRequest);
                }
            }
        }
    }

    public delegate void ProcessDownloadedBlockDelegate(BlockPair blockPair);

    public class BlockDownloadRequest
    {
        public List<uint256> BlocksToDownload { get; set; }

        public ProcessDownloadedBlockDelegate DownloadedBlockDelegate { get; set; }
    }
}