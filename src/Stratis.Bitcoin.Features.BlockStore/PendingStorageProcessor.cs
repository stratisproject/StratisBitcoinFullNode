using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Removes the BlockPairs from PendingStorage and persists the blocks to the hard drive 
    /// or decides to delay persisting if the batch is too small in case we're in IBD.
    /// </summary>
    internal sealed class PendingStorageProcessor
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The block currently being processed.</summary>
        private ChainedBlock nextChainedBlock;
        
        /// <summary>
        /// Used to check if we should break execution when the next block's previous hash doesn't
        /// match this block's hash.
        /// </summary>
        private ChainedBlock previousChainedBlock;

        /// <summary>If this value reaches <see cref="BlockStoreLoop.TargetPendingInsertSize"/> the step will exit./></summary>
        private int pendingStorageBatchSize = 0;

        /// <summary>The last item that was dequeued from <see cref="pendingBlockPairsToStore"/>.</summary>
        private BlockPair pendingBlockPairToStore;

        /// <summary>A collection of blocks that are pending to be pushed to store.</summary>
        private ConcurrentStack<BlockPair> pendingBlockPairsToStore = new ConcurrentStack<BlockPair>();

        private readonly BlockStoreLoop blockStoreLoop;

        /// <summary>The minimum amount of blocks that can be stored in Pending Storage before they get processed.</summary>
        /// <remarks>This value depends on the average block size.</remarks>
        private int pendingStorageBatchThreshold = 10;

        public PendingStorageProcessor(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
        {
            this.blockStoreLoop = blockStoreLoop;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Processes the blockStoreLoop.PendingStorage.</summary>
        public async Task ExecuteAsync(ChainedBlock nextChainedBlock, bool forceFlush)
        {
            this.nextChainedBlock = nextChainedBlock;
            
            bool ibd = this.blockStoreLoop.InitialBlockDownloadState.IsInitialBlockDownload();

            // In case if IBD do not save every single block- persist them in batches.
            if (ibd && this.blockStoreLoop.PendingStorage.Count < this.pendingStorageBatchThreshold && !forceFlush)
                return;

            while (true)
            {
                this.PrepareNextBlockFromPendingStorage();

                if (!this.CanProcessNextBlock())
                    break;

                if (ibd && this.pendingStorageBatchSize > BlockStoreLoop.TargetPendingInsertSize)
                {
                    this.logger.LogDebug("Batch size is {0} bytes, saving the blocks.", this.pendingStorageBatchSize);
                    await this.PushBlocksToRepositoryAsync().ConfigureAwait(false);
                    this.logger.LogDebug("Blocks saved.");
                }
            }

            if (this.pendingBlockPairsToStore.Any())
            {
                this.logger.LogDebug("Pending blocks count: {0}, saving the blocks.", this.pendingBlockPairsToStore.Count);
                await this.PushBlocksToRepositoryAsync().ConfigureAwait(false);
                this.logger.LogDebug("Blocks saved.");
            }
        }
        
        /// <summary>
        /// Tries to get and remove the next block from pending storage. If it exists
        /// then add it to <see cref="pendingBlockPairToStore"/>.
        /// </summary>
        private void PrepareNextBlockFromPendingStorage()
        {
            bool blockIsInPendingStorage = this.blockStoreLoop.PendingStorage.TryRemove(this.nextChainedBlock.HashBlock, out this.pendingBlockPairToStore);
            if (blockIsInPendingStorage)
            {
                this.pendingBlockPairsToStore.Push(this.pendingBlockPairToStore);
                this.pendingStorageBatchSize += this.pendingBlockPairToStore.Block.GetSerializedSize();
            }
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks and set the Store's tip to <see cref="nextChainedBlock"/>
        /// </summary>
        private async Task PushBlocksToRepositoryAsync()
        {
            await this.blockStoreLoop.BlockRepository.PutAsync(this.pendingBlockPairsToStore.First().ChainedBlock.HashBlock, this.pendingBlockPairsToStore.Select(b => b.Block).ToList());
            this.blockStoreLoop.SetStoreTip(this.pendingBlockPairsToStore.First().ChainedBlock);

            // Set blocks threshold.
            int oldThreshold = this.pendingStorageBatchThreshold;
            
            int averageBlockSize = this.pendingStorageBatchSize / this.pendingBlockPairsToStore.Count;
            this.pendingStorageBatchThreshold = BlockStoreLoop.TargetPendingInsertSize / averageBlockSize;

            // Do not allow batch threshold to be changed significantly because there may be an anomaly in the network.
            int maxTimesChange = 10;

            if (this.pendingStorageBatchThreshold > oldThreshold * maxTimesChange)
                this.pendingStorageBatchThreshold = oldThreshold * maxTimesChange;
            else if (this.pendingStorageBatchThreshold * maxTimesChange < oldThreshold)
                this.pendingStorageBatchThreshold = oldThreshold / maxTimesChange;

            this.logger.LogDebug("Pending Storage Batch Threshold is set to: {0}", this.pendingStorageBatchThreshold);

            this.pendingBlockPairToStore = null;
            this.pendingBlockPairsToStore.Clear();
            this.pendingStorageBatchSize = 0;
        }

        /// <summary>
        /// Break execution if:
        /// <list>
        ///     <item>1: Next block is null.</item>
        ///     <item>2: Next block previous hash does not match previous block.</item>
        ///     <item>3: Next block is at tip.</item>
        /// </list>
        /// </summary>
        /// <returns>Returns <c>true</c> if none of the above condition were met, i.e. the next block can be processed.</returns>
        private bool CanProcessNextBlock()
        {
            this.logger.LogTrace("()");

            this.previousChainedBlock = this.nextChainedBlock;
            this.nextChainedBlock = this.blockStoreLoop.Chain.GetBlock(this.nextChainedBlock.Height + 1);

            if (this.nextChainedBlock == null)
            {
                this.logger.LogTrace("(-)[NO_NEXT]:false");
                return false;
            }

            if (this.nextChainedBlock.Header.HashPrevBlock != this.previousChainedBlock.HashBlock)
            {
                this.logger.LogTrace("(-)[REORG]:false");
                return false;
            }

            if (this.nextChainedBlock.Height > this.blockStoreLoop.ChainState.ConsensusTip?.Height)
            {
                this.logger.LogTrace("(-)[NEXT_GT_CONSENSUS_TIP]:false");
                return false;
            }

            this.logger.LogTrace("(-):true");
            return true;
        }
    }
}
