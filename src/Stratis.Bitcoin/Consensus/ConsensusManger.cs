﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// TODO: use this interface on the new block puller.
    /// </summary>
    public interface IBlockPuller
    {
        void NewTipClaimed(int networkPeerId, ChainedHeader chainedHeader);

        long AverageBlockSize { get; }

        void RequestNewData(BlockDownloadRequest downloadRequest);

        void PeerDisconnected(int networkPeerId);
    }

    public class ConsensusManager : IDisposable
    {
        /// <summary>
        /// Maximum memory in bytes that can be taken by the blocks that were downloaded but
        /// not yet validated or included to the consensus chain.
        /// </summary>
        private const long MaxUnconsumedBlocksDataBytes = 200 * 1024 * 1024;

        /// <summary>Queue consumption threshold in bytes.</summary>
        /// <remarks><see cref="toDownloadQueue"/> consumption will start if only we have more than this value of free memory.</remarks>
        private const long ConsumptionThresholdBytes = MaxUnconsumedBlocksDataBytes / 10;

        /// <summary>The default number of blocks to ask when there is no historic data to estimate average block size.</summary>
        private const int DefaultNumberOfBlocksToAsk = 10;

        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly IBlockValidator blockValidator;
        private readonly ConsensusSettings consensusSettings;
        private readonly IBlockPuller blockPuller;
        private readonly IConsensusRules consensusRules;
        private readonly IConnectionManager connectionManager;
        private readonly IBlockStore blockStore;

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader Tip { get; private set; }

        private readonly Dictionary<uint256, List<OnBlockDownloadedCallback>> callbacksByBlocksRequestedHash;

        private readonly Queue<BlockDownloadRequest> toDownloadQueue;
        
        /// <summary>Protects access to the <see cref="blockPuller"/>, <see cref="chainedHeaderTree"/>, <see cref="expectedBlockSizes"/> and <see cref="expectedBlockDataBytes"/>.</summary>
        private readonly object peerLock;

        private readonly object blockRequestedLock;

        private readonly AsyncLock reorgLock;

        private long expectedBlockDataBytes;

        private readonly Dictionary<uint256, long> expectedBlockSizes;

        public ConsensusManager(
            Network network, 
            ILoggerFactory loggerFactory, 
            IChainState chainState, 
            IBlockValidator blockValidator, 
            ICheckpoints checkpoints, 
            ConsensusSettings consensusSettings, 
            IBlockPuller blockPuller,
            IConsensusRules consensusRules,
            IFinalizedBlockHeight finalizedBlockHeight,
            IConnectionManager connectionManager,
            IBlockStore blockStore = null)
        {
            this.network = network;
            this.chainState = chainState;
            this.blockValidator = blockValidator;
            this.consensusSettings = consensusSettings;
            this.blockPuller = blockPuller;
            this.consensusRules = consensusRules;
            this.connectionManager = connectionManager;
            this.blockStore = blockStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, blockValidator, checkpoints, chainState, finalizedBlockHeight, consensusSettings);

            this.peerLock = new object();
            this.reorgLock = new AsyncLock();
            this.blockRequestedLock = new object();
            this.expectedBlockDataBytes = 0;
            this.expectedBlockSizes = new Dictionary<uint256, long>();

            this.callbacksByBlocksRequestedHash = new Dictionary<uint256, List<OnBlockDownloadedCallback>>();
            this.toDownloadQueue = new Queue<BlockDownloadRequest>();
        }

        /// <summary>
        /// Set the tip of <see cref="ConsensusManager"/>, if the given <paramref name="chainTip"/> is not equal to <see cref="Tip"/>
        /// then rewind consensus until a common header is found.
        /// </summary>
        /// <remarks>
        /// If <see cref="blockStore"/> is not <c>null</c> (block store is available) then all block headers in
        /// <see cref="chainedHeaderTree"/> will be marked as their block data is available.
        /// If store is not available the <see cref="ConsensusManager"/> won't be able to serve blocks from disk,
        /// instead all block requests that are not in memory will be sent to the <see cref="blockPuller"/>.
        /// </remarks>
        /// <param name="chainTip">Last common header between chain repository and block store if it's available,
        /// if the store is not available it is the chain repository tip.</param>
        public async Task InitializeAsync(ChainedHeader chainTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainTip), chainTip);

            // TODO: consensus store
            // We should consider creating a consensus store class that will internally contain
            //  coinview and it will abstract the methods `RewindAsync()` `GetBlockHashAsync()` 

            uint256 consensusTipHash = await this.consensusRules.GetBlockHashAsync().ConfigureAwait(false);

            while (true)
            {
                this.Tip = chainTip.FindAncestorOrSelf(consensusTipHash);

                if (this.Tip?.HashBlock == consensusTipHash)
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                consensusTipHash = await this.consensusRules.RewindAsync().ConfigureAwait(false);
            }
            
            this.chainedHeaderTree.Initialize(this.Tip, this.blockStore != null);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peerId">The peer that is providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <returns>The last chained header that is connected to the tree.</returns>
        /// <exception cref="ConnectHeaderException">Thrown when first presented header can't be connected to any known chain in the tree.</exception>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        public ChainedHeader HeadersPresented(int peerId, List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(peerId), peerId, nameof(headers), nameof(headers.Count), headers.Count);

            ConnectNewHeadersResult newHeaders = null;

            lock (this.peerLock)
            {
                newHeaders = this.chainedHeaderTree.ConnectNewHeaders(peerId, headers);
                this.blockPuller.NewTipClaimed(peerId, newHeaders.Consumed);
            }

            if (newHeaders.DownloadTo != null)
            {
                this.DownloadBlocks(newHeaders.ToHashArray(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-):'{0}'", newHeaders.Consumed);
            return newHeaders.Consumed;
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that was disconnected.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.peerLock)
            {
                this.chainedHeaderTree.PeerDisconnected(peerId);
                this.blockPuller.PeerDisconnected(peerId);
                this.ProcessDownloadQueueLocked();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A callback that is triggered when a block that <see cref="ConsensusManager"/> requested was downloaded.
        /// </summary>
        private void ProcessDownloadedBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            bool partialValidationRequired = false;

            lock (this.peerLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block);
            }

            if (partialValidationRequired)
                this.blockValidator.StartPartialValidation(chainedHeaderBlock, this.OnPartialValidationCompletedCallbackAsync);

            this.logger.LogTrace("(-)");
        }

        private async Task OnPartialValidationCompletedCallbackAsync(ChainedHeaderBlock chainedHeaderBlock, bool success)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(chainedHeaderBlock), chainedHeaderBlock, nameof(success), success);

            if (success)
            {
                await this.OnPartialValidationSucceededAsync(chainedHeaderBlock).ConfigureAwait(false);
            }
            else
            {
                List<int> peersToBan;

                lock (this.peerLock)
                {
                    peersToBan = this.chainedHeaderTree.PartialOrFullValidationFailed(chainedHeaderBlock.ChainedHeader);
                }

                this.logger.LogDebug("Validation of block '{0}' failed, banning and disconnecting {1} peers.", chainedHeaderBlock, peersToBan.Count);

                foreach (int peerId in peersToBan)
                {
                    // TODO: ban and disconnect those peers
                }
            }

            this.logger.LogTrace("(-)");
        }

        private async Task OnPartialValidationSucceededAsync(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            bool reorged = false;
            List<ChainedHeader> nextHeadersToValidate = null;

            using (await this.reorgLock.LockAsync().ConfigureAwait(false))
            {
                bool reorgRequired = false;

                lock (this.peerLock)
                {
                    nextHeadersToValidate = this.chainedHeaderTree.PartialValidationSucceeded(chainedHeaderBlock.ChainedHeader, out reorgRequired);
                }

                if (reorgRequired)
                    reorged = await this.FullyValidateAndReorgAsyncLocked().ConfigureAwait(false);
            }

            if (reorged)
                this.NotifyChainedHeaderBehaviorsOnReorg();

            this.logger.LogTrace("Partial validation of {0} block will be started.", nextHeadersToValidate.Count);

            // Start validating all next blocks that come after the current block,
            // all headers in this list have the blocks present in the header.
            foreach (ChainedHeader chainedHeader in nextHeadersToValidate)
            {
                var newChainedHeaderBlock = new ChainedHeaderBlock(chainedHeader.Block, chainedHeader);
                this.blockValidator.StartPartialValidation(newChainedHeaderBlock, this.OnPartialValidationCompletedCallbackAsync);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Notifies the chained header behaviors of all connected peers when a reorg happens.
        /// Consumes headers from their caches if there are any.
        /// </summary>
        private void NotifyChainedHeaderBehaviorsOnReorg()
        {
            this.logger.LogTrace("()");

            var blocksToDownload = new List<ConnectNewHeadersResult>();

            foreach (INetworkPeer peer in this.connectionManager.ConnectedPeers)
            {
                List<ChainedHeader> headersToConnect = peer.Behavior<ChainHeadersBehavior>().ConsensusAdvanced(this.Tip);

                if (headersToConnect == null)
                {
                    this.logger.LogTrace("No cached headers were presented by peer ID {0}.", peer.Connection.Id);
                    continue;
                }

                List<BlockHeader> headers = headersToConnect.Select(ch => ch.Header).ToList();
                this.logger.LogTrace("{0} cached headers were presented by peer ID {1}.", headers.Count, peer.Connection.Id);

                lock (this.peerLock)
                {
                    ConnectNewHeadersResult connectNewHeaders = this.chainedHeaderTree.ConnectNewHeaders(peer.Connection.Id, headers);

                    if (connectNewHeaders.DownloadTo != null)
                        blocksToDownload.Add(connectNewHeaders);
                }
            }
            
            foreach (ConnectNewHeadersResult newHeaders in blocksToDownload)
                this.DownloadBlocks(newHeaders.ToHashArray(), this.ProcessDownloadedBlock);

            this.logger.LogTrace("(-)");
        }

        private async Task<bool> FullyValidateAndReorgAsyncLocked()
        {
            return false;
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="chainedHeaders"/> is not an array of consecutive headers it will be split to batches of consecutive header requests.
        /// Callbacks of all entries are added to <see cref="callbacksByBlocksRequestedHash"/>. If a block header was already requested
        /// to download and not delivered yet, it will not be requested again, instead just it's callback will be called when the block arrives.
        /// </summary>
        /// <param name="chainedHeaders">Array of chained headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        private void DownloadBlocks(ChainedHeader[] chainedHeaders, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(chainedHeaders), nameof(chainedHeaders.Length), chainedHeaders.Length);

            var downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (this.blockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in chainedHeaders)
                {
                    bool blockAlreadyAsked = this.callbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out List<OnBlockDownloadedCallback> callbacks);

                    if (!blockAlreadyAsked)
                    {
                        callbacks = new List<OnBlockDownloadedCallback>();
                        this.callbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, callbacks);
                    }
                    else
                    {
                        this.logger.LogTrace("Registered additional callback for the block '{0}'.", chainedHeader);
                    }
                    
                    callbacks.Add(onBlockDownloadedCallback);
                    
                    bool blockIsNotConsecutive = (previousHeader != null) && (chainedHeader.Previous.HashBlock != previousHeader.HashBlock);

                    if (blockIsNotConsecutive || blockAlreadyAsked)
                    {
                        if (request != null)
                        {
                            downloadRequests.Add(request);
                            request = null;
                        }

                        if (blockAlreadyAsked)
                        {
                            previousHeader = null;
                            continue;
                        }
                    }

                    if (request == null)
                        request = new BlockDownloadRequest { BlocksToDownload = new List<ChainedHeader>() };
                    
                    request.BlocksToDownload.Add(chainedHeader);
                    previousHeader = chainedHeader;
                }

                if (request != null)
                    downloadRequests.Add(request);

                lock (this.peerLock)
                {
                    foreach (BlockDownloadRequest downloadRequest in downloadRequests)
                        this.toDownloadQueue.Enqueue(downloadRequest);

                    this.ProcessDownloadQueueLocked();
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void BlockDownloaded(Block block, uint256 blockHash, int peerId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(blockHash), blockHash, nameof(peerId), peerId);

            ChainedHeader chainedHeader = null;
            
            lock (this.peerLock)
            {
                if (this.expectedBlockSizes.TryGetValue(blockHash, out long expectedSize))
                {
                    this.expectedBlockDataBytes -= expectedSize;
                    this.expectedBlockSizes.Remove(blockHash);
                    this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
                }
                else
                {
                    // This means the puller has not filtered blocks correctly.
                    this.logger.LogError("Unsolicited block '{0}'.", blockHash);
                    this.logger.LogTrace("(-)[UNSOLICITED_BLOCK]");
                    throw new InvalidOperationException("Unsolicited block");
                }

                if (block != null)
                {
                    try
                    {
                        chainedHeader = this.chainedHeaderTree.FindHeaderAndVerifyBlockIntegrity(block);
                    }
                    catch (BlockDownloadedForMissingChainedHeaderException)
                    {
                        this.logger.LogTrace("(-)[CHAINED_HEADER_NOT_FOUND]");
                        return;
                    }
                    //catch (BlockIntegrityVerificationException)
                    //{
                    //    // TODO: catch validation exceptions.
                    //    // TODO ban the peer, disconnect, return
                    //    // this.logger.LogTrace("(-)[INTEGRITY_VERIFICATION_FAILED]");
                    //    return;
                    //}
                }
                else
                {
                    this.logger.LogDebug("Block '{0}' failed to be delivered.", blockHash);
                }
            }

            List<OnBlockDownloadedCallback> listOfCallbacks = null;

            lock (this.blockRequestedLock)
            {
                if (this.callbacksByBlocksRequestedHash.TryGetValue(blockHash, out listOfCallbacks))
                    this.callbacksByBlocksRequestedHash.Remove(blockHash);
            }

            if (listOfCallbacks != null)
            {
                ChainedHeaderBlock chainedHeaderBlock = null;

                if (block != null)
                    chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

                this.logger.LogTrace("Calling {0} callbacks for block '{1}'.", listOfCallbacks.Count, chainedHeader);
                foreach (OnBlockDownloadedCallback blockDownloadedCallback in listOfCallbacks)
                    blockDownloadedCallback(chainedHeaderBlock);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Provides block data for the given block hashes.
        /// </summary>
        /// <remarks>
        /// First we check if the block exists in chained header tree, then it check the block store and if it wasn't found there the block will be scheduled for download.
        /// Given callback is called when the block is obtained. If obtaining the block fails the callback will be called with <c>null</c>.
        /// </remarks>
        /// <param name="blockHashes">The block hashes to download.</param>
        /// <param name="onBlockDownloadedCallback">The callback that will be called for each downloaded block.</param>
        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

            var blocksToDownload = new List<ChainedHeader>();

            for (int i = 0; i < blockHashes.Count; i++)
            {
                uint256 blockHash = blockHashes[i];
                ChainedHeaderBlock chainedHeaderBlock = null;

                lock (this.peerLock)
                {
                    chainedHeaderBlock = this.chainedHeaderTree.GetChainedHeaderBlock(blockHash);
                }

                if (chainedHeaderBlock == null)
                {
                    this.logger.LogTrace("Block hash '{0}' is not part of the tree.", blockHash);
                    onBlockDownloadedCallback(null);
                    continue;
                }

                if (chainedHeaderBlock.Block != null)
                {
                    this.logger.LogTrace("Block pair '{0}' was found in memory.", chainedHeaderBlock);
                    onBlockDownloadedCallback(chainedHeaderBlock);
                    continue;
                }

                if (this.blockStore != null)
                {
                    Block block = await this.blockStore.GetBlockAsync(blockHash).ConfigureAwait(false);
                    if (block != null)
                    {
                        var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                        this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                        onBlockDownloadedCallback(newBlockPair);
                        continue;
                    }
                }

                blocksToDownload.Add(chainedHeaderBlock.ChainedHeader);
                this.logger.LogTrace("Block hash '{0}' is queued for download.", blockHash);
            }

            if (blocksToDownload.Count != 0)
            {
                this.logger.LogTrace("Asking block puller for {0} blocks.", blocksToDownload.Count);
                this.DownloadBlocks(blocksToDownload.ToArray(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes items in the <see cref="toDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks we will not ask block puller for more until some blocks are consumed.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be split in batches.
        /// The amount of blocks in 1 batch to downloaded depends on the average value in <see cref="IBlockPuller.AverageBlockSize"/>.
        /// </remarks>
        private void ProcessDownloadQueueLocked()
        {
            this.logger.LogTrace("()");
            
            while (this.toDownloadQueue.Count > 0)
            {
                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                long freeBytes = MaxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes - this.expectedBlockDataBytes;
                this.logger.LogTrace("{0} bytes worth of blocks is available for download.", freeBytes);

                if (freeBytes <= ConsumptionThresholdBytes)
                {
                    this.logger.LogTrace("(-)[THRESHOLD_NOT_MET]");
                    return;
                }

                long avgSize = this.blockPuller.AverageBlockSize;
                int blocksToAsk = avgSize != 0 ? (int)(freeBytes / avgSize) : DefaultNumberOfBlocksToAsk;

                this.logger.LogTrace("With {0} average block size, we have {1} download slots available.", avgSize, blocksToAsk);

                if (request.BlocksToDownload.Count <= blocksToAsk)
                {
                    this.toDownloadQueue.Dequeue();
                }
                else
                {
                    this.logger.LogTrace("Splitting enqueued job of size {0} into 2 pieces of sizes {1} and {2}.", request.BlocksToDownload.Count, blocksToAsk, request.BlocksToDownload.Count - blocksToAsk);

                    // Split queue item in 2 pieces: one of size blocksToAsk and second is the rest. Ask BP for first part, leave 2nd part in the queue.
                    var blockPullerRequest = new BlockDownloadRequest()
                    {
                        BlocksToDownload = new List<ChainedHeader>(request.BlocksToDownload.GetRange(0, blocksToAsk))
                    };

                    request.BlocksToDownload.RemoveRange(0, blocksToAsk);

                    request = blockPullerRequest;
                }

                this.blockPuller.RequestNewData(request);

                foreach (ChainedHeader chainedHeader in request.BlocksToDownload)
                    this.expectedBlockSizes.Add(chainedHeader.HashBlock, avgSize);

                this.expectedBlockDataBytes += request.BlocksToDownload.Count * avgSize;

                this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.reorgLock.Dispose();
        }
    }

    /// <summary>
    /// A delegate that is used to send callbacks when a bock is downloaded from the of queued requests to downloading blocks. 
    /// </summary>
    /// <param name="chainedHeaderBlock">The pair of the block and its chained header.</param>
    public delegate void OnBlockDownloadedCallback(ChainedHeaderBlock chainedHeaderBlock);

    /// <summary>
    /// A request that holds information of blocks to download.
    /// </summary>
    public class BlockDownloadRequest
    {
        /// <summary>The list of block headers to download.</summary>
        public List<ChainedHeader> BlocksToDownload { get; set; }
    }
}