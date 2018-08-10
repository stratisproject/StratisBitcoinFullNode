using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class DownloadBlocksVisitor : IConsensusVisitor
    {
        public List<uint256> BlockHashes { get; set; }

        private readonly ILogger logger;

        public OnBlockDownloadedCallback OnBlockDownloadedCallback { get; set; }

        public bool TriggerDownload { get; set; }

        public DownloadBlocksVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.TriggerDownload = true;
        }

        public async ConsensusVisitorResult Visit(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(this.BlockHashes), nameof(this.BlockHashes.Count), this.BlockHashes.Count);

            var blocksToDownload = new List<ChainedHeader>();

            foreach (uint256 blockHash in this.BlockHashes)
            {
                ChainedHeaderBlock chainedHeaderBlock = await this.LoadBlockDataAsync(consensusManager, blockHash).ConfigureAwait(false);

                if ((chainedHeaderBlock == null) || (chainedHeaderBlock.Block != null))
                {
                    if (chainedHeaderBlock != null)
                        this.logger.LogTrace("Block data loaded for hash '{0}', calling the callback.", blockHash);
                    else
                        this.logger.LogTrace("Chained header not found for hash '{0}'.", blockHash);

                    this.OnBlockDownloadedCallback(chainedHeaderBlock);
                }
                else
                {
                    blocksToDownload.Add(chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Block hash '{0}' is queued for download.", blockHash);
                }
            }

            if (blocksToDownload.Count != 0)
            {
                this.logger.LogTrace("Asking block puller for {0} blocks.", blocksToDownload.Count);
                this.DownloadBlocks(consensusManager, blocksToDownload.ToArray(), consensusManager.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Loads the block data from <see cref="ChainedHeaderTree"/> or block store if it's enabled.</summary>
        /// <param name="blockHash">The block hash.</param>
        private async Task<ChainedHeaderBlock> LoadBlockDataAsync(ConsensusManager consensusManager, uint256 blockHash)
        {
            this.logger.LogTrace("({0}:{1})", nameof(blockHash), blockHash);

            ChainedHeaderBlock chainedHeaderBlock;

            lock (consensusManager.PeerLock)
            {
                chainedHeaderBlock = consensusManager.ChainedHeaderTree.GetChainedHeaderBlock(blockHash);
            }

            if (chainedHeaderBlock == null)
            {
                this.logger.LogTrace("Block hash '{0}' is not part of the tree.", blockHash);
                this.logger.LogTrace("(-)[INVALID_HASH]:null");
                return null;
            }

            if (chainedHeaderBlock.Block != null)
            {
                this.logger.LogTrace("Block pair '{0}' was found in memory.", chainedHeaderBlock);

                this.logger.LogTrace("(-)[FOUND_IN_CHT]:'{0}'", chainedHeaderBlock);
                return chainedHeaderBlock;
            }

            if (consensusManager.BlockStore != null)
            {
                Block block = await consensusManager.BlockStore.GetBlockAsync(blockHash).ConfigureAwait(false);
                if (block != null)
                {
                    var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                    this.logger.LogTrace("(-)[FOUND_IN_BLOCK_STORE]:'{0}'", newBlockPair);
                    return newBlockPair;
                }
            }

            this.logger.LogTrace("(-)[NOT_FOUND]:'{0}'", chainedHeaderBlock);
            return chainedHeaderBlock;
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="chainedHeaders"/> is not an array of consecutive headers it will be split to batches of consecutive header requests.
        /// Callbacks of all entries are added to <see cref="callbacksByBlocksRequestedHash"/>. If a block header was already requested
        /// to download and not delivered yet, it will not be requested again, instead just it's callback will be called when the block arrives.
        /// </summary>
        /// <param name="chainedHeaders">Array of chained headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        private void DownloadBlocks(ConsensusManager consensusManager, ChainedHeader[] chainedHeaders, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(chainedHeaders), nameof(chainedHeaders.Length), chainedHeaders.Length);

            var downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (consensusManager.BlockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in chainedHeaders)
                {
                    bool blockAlreadyAsked = consensusManager.CallbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out List<OnBlockDownloadedCallback> callbacks);

                    if (!blockAlreadyAsked)
                    {
                        callbacks = new List<OnBlockDownloadedCallback>();
                        consensusManager.CallbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, callbacks);
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

                lock (consensusManager.PeerLock)
                {
                    foreach (BlockDownloadRequest downloadRequest in downloadRequests)
                        consensusManager.ToDownloadQueue.Enqueue(downloadRequest);

                    consensusManager.ProcessDownloadQueueLocked();
                }
            }

            this.logger.LogTrace("(-)");
        }
    }
}
