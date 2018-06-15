using System;
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

    public class ConsensusManager
    {
        const long MaxUnconsumedBlocksDataBytes = 1048576 * 500; // 500 mb

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

        private readonly object treeLock;

        private readonly object blockRequestedLock;

        private readonly object reorgLock;

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

            this.treeLock = new object();
            this.reorgLock = new object();
            this.blockRequestedLock = new object();

            this.callbacksByBlocksRequestedHash = new Dictionary<uint256, List<OnBlockDownloadedCallback>>();
            this.toDownloadQueue = new Queue<BlockDownloadRequest>();
        }

        /// <summary>
        /// Set the tip of <see cref="ConsensusManager"/>, if the given <paramref name="chainTip"/> is not similar to <see cref="Tip"/>
        /// then rewind consensus until a common header is found.
        /// </summary>
        /// <remarks>
        /// If <see cref="blockStore"/> is not <c>null</c> (block store is enabled) then all block headers in <see cref="chainedHeaderTree"/> will be marked as their block data is available.
        /// If store is disabled the <see cref="ConsensusManager"/> won't be able to serve blocks from disk, instead all block requests that are not in memory will be sent to the <see cref="IBlockPuller"/>.
        /// </remarks>
        /// <param name="chainTip">The consensus tip that is attempted to be set.</param>
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

            lock (this.treeLock)
            {
                this.chainedHeaderTree.Initialize(this.Tip, this.blockStore != null);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peerId">The peer that is providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <returns>The last chained header that is connected to the tree.</returns>
        public ChainedHeader HeadersPresented(int peerId, List<BlockHeader> headers)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4})", nameof(peerId), peerId, nameof(headers), nameof(headers.Count), headers.Count);

            ConnectNewHeadersResult newHeaders = null;

            lock (this.treeLock)
            {
                newHeaders = this.chainedHeaderTree.ConnectNewHeaders(peerId, headers);
                this.blockPuller.NewTipClaimed(peerId, newHeaders.Consumed);
            }

            if (newHeaders.DownloadTo != null)
            {
                this.DownloadBlocks(newHeaders.ToHashArray().ToList(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-):'{0}'", newHeaders?.Consumed);
            return newHeaders.Consumed;
        }

        /// <summary>
        /// A peer was disconnected, clean all its claimed headers.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that is disconnecting.</param>
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
        /// A callback that is triggered when a block that <see cref="ConsensusManager"/> requested was downloaded.
        /// </summary>
        private void ProcessDownloadedBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            bool partialValidationRequired = false;

            lock (this.treeLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block);
            }

            if (partialValidationRequired)
            {
                this.blockValidator.StartPartialValidation(chainedHeaderBlock, this.OnPartialValidationCompletedCallback);
            }

            this.logger.LogTrace("(-)");
        }

        private void OnPartialValidationCompletedCallback(ChainedHeaderBlock chainedHeaderBlock, bool success)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(chainedHeaderBlock), chainedHeaderBlock, nameof(success), success);

            if (success)
            {
                this.OnPartialValidationSucceeded(chainedHeaderBlock);
            }
            else
            {
                List<int> peersToBan;

                lock (this.treeLock)
                {
                    peersToBan = this.chainedHeaderTree.PartialOrFullValidationFailed(chainedHeaderBlock.ChainedHeader);
                }

                foreach (int peerId in peersToBan)
                {
                    // TODO: ban and disconnect those peers
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void OnPartialValidationSucceeded(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            bool reorged = false;
            List<ChainedHeader> nextHeadersToValidate = null;

            lock (this.reorgLock)
            {
                bool reorgRequired = false;

                lock (this.treeLock)
                {
                    nextHeadersToValidate = this.chainedHeaderTree.PartialValidationSucceeded(chainedHeaderBlock.ChainedHeader, out reorgRequired);
                }

                if (reorgRequired)
                {
                    reorged = this.FullyValidateAndReorgLocked();
                }
            }

            if (reorged)
            {
                List<ConnectNewHeadersResult> blocksToDownload = new List<ConnectNewHeadersResult>();

                var peers = this.connectionManager.ConnectedPeers;

                foreach (INetworkPeer peer in peers)
                {
                    List<ChainedHeader> headersToConnect =  peer.Behavior<ChainHeadersBehavior>().ConsensusAdvanced(this.Tip);

                    if (headersToConnect != null)
                    {
                        List<BlockHeader> headers = headersToConnect.Select(ch => ch.Header).ToList();

                        lock (this.treeLock)
                        {
                            ConnectNewHeadersResult connectNewHeaders = this.chainedHeaderTree.ConnectNewHeaders(peer.Connection.Id, headers);

                            if (connectNewHeaders.DownloadTo != null)
                            {
                                blocksToDownload.Add(connectNewHeaders);
                            }
                        }
                    }
                }

                foreach (ConnectNewHeadersResult newHeaders in blocksToDownload)
                {
                    this.DownloadBlocks(newHeaders.ToHashArray().ToList(), this.ProcessDownloadedBlock);
                }
            }

            // Start validating all next blocks that come after the current block,
            // all headers in this list have the blocks present in the header.
            foreach (ChainedHeader chainedHeader in nextHeadersToValidate)
            {
                var newChainedHeaderBlock = new ChainedHeaderBlock(chainedHeader.Block, chainedHeader);
                this.blockValidator.StartPartialValidation(newChainedHeaderBlock, this.OnPartialValidationCompletedCallback);
            }

            this.logger.LogTrace("(-)");
        }

        private bool FullyValidateAndReorgLocked()
        {
            return false;
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="blockHashes"/> is not consecutive the list will be split to batches of consecutive header requests.
        /// if a block header was already requested to download its delegate will be added to existing entry in <see cref="callbacksByBlocksRequestedHash"/>.
        /// </summary>
        /// <param name="blockHashes">List of headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        private void DownloadBlocks(List<ChainedHeader> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

            List<BlockDownloadRequest> downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (this.blockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in blockHashes)
                {
                    bool blockAlreadyAsked = false;
                    if (this.callbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out List<OnBlockDownloadedCallback> listOfCallbacks))
                    {
                        listOfCallbacks.Add(onBlockDownloadedCallback);
                        blockAlreadyAsked = true;
                    }
                    else
                    {
                        this.callbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, new List<OnBlockDownloadedCallback>() { onBlockDownloadedCallback });
                    }

                    bool blockIsNotConsecutive = previousHeader != null && chainedHeader.Previous.HashBlock != previousHeader.HashBlock;

                    if (blockIsNotConsecutive || blockAlreadyAsked)
                    {
                        if (request != null)
                        {
                            downloadRequests.Add(request);
                        }

                        if (blockAlreadyAsked)
                        {
                            previousHeader = null;
                            continue;
                        }
                    }

                    if (request == null)
                        request = new BlockDownloadRequest { BlocksToDownload = new List<ChainedHeader>() };

                    previousHeader = chainedHeader;
                    request.BlocksToDownload.Add(chainedHeader);
                }

                if (request != null)
                {
                    downloadRequests.Add(request);
                }

                lock (this.treeLock)
                {
                    foreach (BlockDownloadRequest downloadRequest in downloadRequests)
                    {
                        this.toDownloadQueue.Enqueue(downloadRequest);
                        this.ProcessDownloadQueueLocked();
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void BlockDownloaded(Block block, uint256 blockHash, int peerId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(blockHash), blockHash, nameof(peerId), peerId);

            ChainedHeader chainedHeader = null;
            if (block != null)
            {
                lock (this.treeLock)
                {
                    try
                    {
                        chainedHeader = this.chainedHeaderTree.FindHeaderAndVerifyBlockIntegrity(block);
                    }
                    catch (ConsensusException cex)
                    {
                        switch (cex)
                        {
                            case BlockDownloadedForMissingChainedHeaderException bcex:
                                return;

                            // TODO: catch validation exceptions.
                        }
                    }
                }
            }

            List<OnBlockDownloadedCallback> listOfCallbacks = null;

            lock (this.blockRequestedLock)
            {
                if (this.callbacksByBlocksRequestedHash.TryGetValue(blockHash, out listOfCallbacks))
                {
                    this.callbacksByBlocksRequestedHash.Remove(blockHash);
                }
            }

            if (listOfCallbacks != null)
            {
                foreach (var blockDownloadedCallback in listOfCallbacks)
                {
                    ChainedHeaderBlock chainedHeaderBlock = null;

                    if (block != null)
                    {
                        chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);
                    }

                    blockDownloadedCallback(chainedHeaderBlock);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A method that to requests blocks, any block that is found is sent to the callback <see cref="onBlockDownloadedCallback"/>.
        /// If the block is not part of the tree (on a known the chain) the a <c>null</c> value will be return in the callback.
        /// If the block is available in memory or on disk it will be immediately return the block on the callback.
        /// The block hashes must be in a consecutive order.
        /// </summary>
        /// <param name="blockHashes">The block hashes to download, the block hashes must be in a consecutive order.</param>
        /// <param name="onBlockDownloadedCallback">The callback that will be called for each downloaded block.</param>
        /// <returns></returns>
        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

            List<ChainedHeader> downloadRequests = new List<ChainedHeader>();

            for (int i = blockHashes.Count - 1; i >= 0; i--)
            {
                uint256 blockHash = blockHashes[i];
                ChainedHeaderBlock chainedHeaderBlock = null;

                lock (this.treeLock)
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
                    Block block = await this.blockStore.GetBlockAsync(blockHash);
                    if (block != null)
                    {
                        var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                        this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                        onBlockDownloadedCallback(newBlockPair);
                        continue;
                    }
                }

                downloadRequests.Add(chainedHeaderBlock.ChainedHeader);
                this.logger.LogTrace("Block hash '{0}' is queued for download.", blockHash);
            }

            this.DownloadBlocks(downloadRequests, this.ProcessDownloadedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes items in <see cref="toDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks or the download will wait, the amount of blocks to downloaded depends on the avg value in <see cref="IBlockPuller.AverageBlockSize"/>.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be split in batches.
        /// </remarks>
        private void ProcessDownloadQueueLocked()
        {
            this.logger.LogTrace("()");


            while (this.toDownloadQueue.Count > 0)
            {
                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                long freeMb = MaxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes;
                this.logger.LogTrace("There is {0} MB available do download is.", freeMb);

                if (freeMb <= 32)
                    return;

                long blocksToAsk = freeMb / this.blockPuller.AverageBlockSize;
                this.logger.LogTrace("The slot of available blocks to ask is {0}.", blocksToAsk);

                if (request.BlocksToDownload.Count <= blocksToAsk)
                {
                    this.toDownloadQueue.Dequeue();
                    this.blockPuller.RequestNewData(request);
                }
                else
                {
                    // split queue item in 2 pieces - one if size blocksToAsk and second is rest.Ask BP for first part, leave 2nd part in queue

                    var newRequest = new BlockDownloadRequest()
                    {
                        BlocksToDownload = new List<ChainedHeader>()
                    };

                    for (int i = 0; i < blocksToAsk; i++)
                    {
                        ChainedHeader chainedHeader = request.BlocksToDownload[0];
                        request.BlocksToDownload.Remove(chainedHeader);
                        newRequest.BlocksToDownload.Add(chainedHeader);
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