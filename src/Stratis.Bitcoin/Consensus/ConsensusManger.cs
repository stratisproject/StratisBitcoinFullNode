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

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peerId">The peer that is providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <returns>The last chained header that is connected to the tree.</returns>
        public ChainedHeader HeadersPresentedAsync(int peerId, List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(peerId), peerId, nameof(headers), nameof(headers.Count), headers.Count);

            ConnectNewHeadersResult newHeaders = null;

            lock (this.treeLock)
            {
                newHeaders = this.chainedHeaderTree.ConnectNewHeaders(peerId, headers);
                this.blockPuller.NewTipClaimed();
            }

            if (newHeaders?.DownloadTo != null)
            {
                this.DownloadBlocks(newHeaders.ToHashList(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-):'{0}'", newHeaders?.Consumed);
            return newHeaders?.Consumed;
        }

        /// <summary>
        /// A peer was disconnected, clean all its claimed headers that are above our consensus tip.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that is providing the headers.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.treeLock)
            {
                this.chainedHeaderTree.PeerDisconnected(peerId);
                this.blockPuller.PeerDisconnected(peerId);
                this.ProcessDownloadQueueLocked();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A callback that is triggered when a block was downloaded.
        /// </summary>
        private void ProcessDownloadedBlock(BlockPair blockPair)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockPair), blockPair);

            bool partialValidationRequired = false;

            lock (this.treeLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(blockPair.ChainedHeader, blockPair.Block);
            }

            if (partialValidationRequired)
            {
                this.chainedHeaderValidator.StartPartialValidation(blockPair, this.OnPartialValidationCompletedCallback);
            }

            this.logger.LogTrace("(-)");
        }

        private void OnPartialValidationCompletedCallback(BlockPair blockPair, bool success)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(blockPair), blockPair, nameof(success), success);

            // TODO

            this.logger.LogTrace("(-)");
        }

        private void DownloadBlocks(List<uint256> blockHashes, ProcessDownloadedBlockDelegate processesBlock)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A method that to requests blocks, any block that is found is sent to the callback <see cref="processesBlock"/>.
        /// If the block is not part of the tree (on a known the chain) the a <c>null</c> value will be return in the callback.
        /// If the block is available in memory or on disk it will be immediately return the block on the callback.
        /// The block hashes must be in a consecutive order.
        /// </summary>
        /// <param name="blockHashes">The block hashes to download, the block hashes must be in a consecutive order.</param>
        /// <param name="processesBlock">The callback that will be called for each downloaded block.</param>
        /// <returns></returns>
        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, ProcessDownloadedBlockDelegate processesBlock)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes items in <see cref="toDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks or the download will wait, the amount of blocks to downloaded depends on the avg value in <see cref="IBlockPuller.AverageBlockSize"/>.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be plit in batches.
        /// </remarks>
        private void ProcessDownloadQueueLocked()
        {
            this.logger.LogTrace("()");

            const long MaxUnconsumedBlocksDataBytes = 1048576 * 10; // 10 mb

            while (this.toDownloadQueue.Count > 0)
            {
                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                long freeMb = MaxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes;
                this.logger.LogTrace("There is {0} MB available do download is.", freeMb);

                if (freeMb <= 32)
                    return;

                long blocksToAsk = freeMb / this.blockPuller.AverageBlockSize;
                this.logger.LogTrace("The slot of available blocks to ask is {0}.", blocksToAsk);

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

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// A delegate that is used to send callbacks when a bock is downloaded from the of queued requests to downloading blocks. 
    /// </summary>
    /// <param name="blockPair">The pair of the block and its chained header.</param>
    public delegate void ProcessDownloadedBlockDelegate(BlockPair blockPair);

    /// <summary>
    /// A request that holds information of blocks to download.
    /// </summary>
    public class BlockDownloadRequest
    {
        /// <summary>The list of block headers to download.</summary>
        public List<uint256> BlocksToDownload { get; set; }

        /// <summary>The delegate to send the downloaded blocks to.</summary>
        public ProcessDownloadedBlockDelegate DownloadedBlockDelegate { get; set; }
    }
}