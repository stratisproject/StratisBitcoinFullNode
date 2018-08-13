using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Facilitates block downloading functionality for <see cref="ConsensusManager"/>.
    /// </summary>
    public sealed class ConsensusBlockDownloader
    {
        private readonly ILogger logger;

        /// <summary>The default number of blocks to ask when there is no historic data to estimate average block size.</summary>
        private const int DefaultNumberOfBlocksToAsk = 10;

        private readonly ConsensusManager consensusManager;
        private readonly Dictionary<uint256, long> expectedBlockSizes;
        private long expectedBlockDataBytes;

        /// <summary>
        /// Maximum memory in bytes that can be taken by the blocks that were downloaded but
        /// not yet validated or included to the consensus chain.
        /// </summary>
        private const long MaxUnconsumedBlocksDataBytes = 200 * 1024 * 1024;

        /// <summary>Queue consumption threshold in bytes.</summary>
        /// <remarks><see cref="ToDownloadQueue"/> consumption will start if only we have more than this value of free memory.</remarks>
        private const long ConsumptionThresholdBytes = MaxUnconsumedBlocksDataBytes / 10;

        /// <summary>The maximum amount of blocks that can be assigned to <see cref="IBlockPuller"/> at the same time.</summary>
        private const int MaxBlocksToAskFromPuller = 5000;

        /// <summary>The minimum amount of slots that should be available to trigger asking block puller for blocks.</summary>
        private const int ConsumptionThresholdSlots = MaxBlocksToAskFromPuller / 10;

        public ConsensusBlockDownloader(ConsensusManager consensusManager, ILoggerFactory loggerFactory)
        {
            this.consensusManager = consensusManager;

            this.expectedBlockDataBytes = 0;
            this.expectedBlockSizes = new Dictionary<uint256, long>();

            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="chainedHeaders"/> is not an array of consecutive headers it will be split to batches of consecutive header requests.
        /// Callbacks of all entries are added to <see cref="callbacksByBlocksRequestedHash"/>. If a block header was already requested
        /// to download and not delivered yet, it will not be requested again, instead just it's callback will be called when the block arrives.
        /// </summary>
        /// <param name="chainedHeaders">Array of chained headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        public void DownloadBlocks(ChainedHeader[] chainedHeaders, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(chainedHeaders), nameof(chainedHeaders.Length), chainedHeaders.Length);

            var downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (this.consensusManager.BlockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in chainedHeaders)
                {
                    bool blockAlreadyAsked = this.consensusManager.CallbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out List<OnBlockDownloadedCallback> callbacks);

                    if (!blockAlreadyAsked)
                    {
                        callbacks = new List<OnBlockDownloadedCallback>();
                        this.consensusManager.CallbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, callbacks);
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

                lock (this.consensusManager.PeerLock)
                {
                    foreach (BlockDownloadRequest downloadRequest in downloadRequests)
                        this.consensusManager.ToDownloadQueue.Enqueue(downloadRequest);

                    this.ProcessDownloadQueueLocked();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A callback that is triggered when a block that <see cref="ConsensusManager"/> requested was downloaded.
        /// </summary>
        public void ProcessDownloadedBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            if (chainedHeaderBlock == null)
            {
                // Peers failed to deliver the block.
                this.logger.LogTrace("(-)[DOWNLOAD_FAILED]");
                return;
            }

            bool partialValidationRequired = false;

            lock (this.consensusManager.PeerLock)
            {
                partialValidationRequired = this.consensusManager.ChainedHeaderTree.BlockDataDownloaded(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block);
            }

            this.logger.LogTrace("Partial validation is{0} required.", partialValidationRequired ? string.Empty : " NOT");

            if (partialValidationRequired)
                this.consensusManager.PartialValidator.StartPartialValidation(chainedHeaderBlock, this.consensusManager.OnPartialValidationCompletedCallbackAsync);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes items in the <see cref="ToDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks we will not ask block puller for more until some blocks are consumed.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be split in batches.
        /// The amount of blocks in 1 batch to downloaded depends on the average value in <see cref="IBlockPuller.GetAverageBlockSizeBytes"/>.
        /// </remarks>
        public void ProcessDownloadQueueLocked()
        {
            this.logger.LogTrace("()");

            while (this.consensusManager.ToDownloadQueue.Count > 0)
            {
                int awaitingBlocksCount = this.expectedBlockSizes.Count;

                int freeSlots = MaxBlocksToAskFromPuller - awaitingBlocksCount;
                this.logger.LogTrace("{0} slots are available.", freeSlots);

                if (freeSlots < ConsumptionThresholdSlots)
                {
                    this.logger.LogTrace("(-)[NOT_ENOUGH_SLOTS]");
                    return;
                }

                long freeBytes = MaxUnconsumedBlocksDataBytes - this.consensusManager.ChainedHeaderTree.UnconsumedBlocksDataBytes - this.expectedBlockDataBytes;
                this.logger.LogTrace("{0} bytes worth of blocks is available for download.", freeBytes);

                if (freeBytes <= ConsumptionThresholdBytes)
                {
                    this.logger.LogTrace("(-)[THRESHOLD_NOT_MET]");
                    return;
                }

                long avgSize = (long)this.consensusManager.BlockPuller.GetAverageBlockSizeBytes();
                int maxBlocksToAsk = avgSize != 0 ? (int)(freeBytes / avgSize) : DefaultNumberOfBlocksToAsk;

                if (maxBlocksToAsk > freeSlots)
                    maxBlocksToAsk = freeSlots;

                this.logger.LogTrace("With {0} average block size, we have {1} download slots available.", avgSize, maxBlocksToAsk);

                BlockDownloadRequest request = this.consensusManager.ToDownloadQueue.Peek();

                if (request.BlocksToDownload.Count <= maxBlocksToAsk)
                {
                    this.consensusManager.ToDownloadQueue.Dequeue();
                }
                else
                {
                    this.logger.LogTrace("Splitting enqueued job of size {0} into 2 pieces of sizes {1} and {2}.", request.BlocksToDownload.Count, maxBlocksToAsk, request.BlocksToDownload.Count - maxBlocksToAsk);

                    // Split queue item in 2 pieces: one of size blocksToAsk and second is the rest. Ask BP for first part, leave 2nd part in the queue.
                    var blockPullerRequest = new BlockDownloadRequest()
                    {
                        BlocksToDownload = new List<ChainedHeader>(request.BlocksToDownload.GetRange(0, maxBlocksToAsk))
                    };

                    request.BlocksToDownload.RemoveRange(0, maxBlocksToAsk);

                    request = blockPullerRequest;
                }

                this.consensusManager.BlockPuller.RequestBlocksDownload(request.BlocksToDownload);

                foreach (ChainedHeader chainedHeader in request.BlocksToDownload)
                    this.expectedBlockSizes.Add(chainedHeader.HashBlock, avgSize);

                this.expectedBlockDataBytes += request.BlocksToDownload.Count * avgSize;

                this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
            }

            this.logger.LogTrace("(-)");
        }

        public void BlockDownloaded(uint256 blockHash, Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            ChainedHeader chainedHeader = null;

            lock (this.consensusManager.PeerLock)
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
                        chainedHeader = this.consensusManager.ChainedHeaderTree.FindHeaderAndVerifyBlockIntegrity(block);
                    }
                    catch (BlockDownloadedForMissingChainedHeaderException)
                    {
                        this.logger.LogTrace("(-)[CHAINED_HEADER_NOT_FOUND]");
                        return;
                    }

                    // catch (BlockIntegrityVerificationException)
                    // {
                    //    // TODO: catch validation exceptions.
                    //    // TODO ban the peer, disconnect, return
                    //    // this.logger.LogTrace("(-)[INTEGRITY_VERIFICATION_FAILED]");
                    //    return;
                    // }
                }
                else
                {
                    this.logger.LogDebug("Block '{0}' failed to be delivered.", blockHash);
                }
            }

            List<OnBlockDownloadedCallback> listOfCallbacks = null;

            lock (this.consensusManager.BlockRequestedLock)
            {
                if (this.consensusManager.CallbacksByBlocksRequestedHash.TryGetValue(blockHash, out listOfCallbacks))
                    this.consensusManager.CallbacksByBlocksRequestedHash.Remove(blockHash);
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
    }
}
