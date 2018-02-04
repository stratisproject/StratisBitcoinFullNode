using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Removes the BlockPairs from PendingStorage and persists the blocks to the hard drive 
    /// or decides to delay persisting if the batch is too small in case we're in IBD.
    /// </summary>
    internal sealed class ProcessPendingStorageStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The block currently being processed.</summary>
        private ChainedBlock nextChainedBlock;

        private CancellationToken cancellationToken;

        /// <summary>
        /// Used to check if we should break execution when the next block's previous hash doesn't
        /// match this block's hash.
        /// </summary>
        private ChainedBlock previousChainedBlock;

        /// <summary>If this value reaches <see cref="BlockStoreLoop.MaxPendingInsertBlockSize"/> the step will exit./></summary>
        private int pendingStorageBatchSize = 0;

        /// <summary>The last item that was dequeued from <see cref="pendingBlockPairsToStore"/>.</summary>
        private BlockPair pendingBlockPairToStore;

        /// <summary>A collection of blocks that are pending to be pushed to store.</summary>
        private ConcurrentStack<BlockPair> pendingBlockPairsToStore = new ConcurrentStack<BlockPair>();

        private readonly BlockStoreLoop blockStoreLoop;

        public ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
        {
            this.blockStoreLoop = blockStoreLoop;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Processes the blockStoreLoop.PendingStorage.</summary>
        public async Task ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken)
        {
            this.nextChainedBlock = nextChainedBlock;
            this.cancellationToken = cancellationToken;
            
            bool ibd = this.blockStoreLoop.InitialBlockDownloadState.IsInitialBlockDownload();

            if (ibd && this.blockStoreLoop.PendingStorage.Count < BlockStoreLoop.PendingStorageBatchThreshold)
                return;

            while (this.cancellationToken.IsCancellationRequested == false)
            {
                this.PrepareNextBlockFromPendingStorage();

                if (!this.CanProcessNextBlock())
                    break;

                if (ibd && this.pendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize)
                    await this.PushBlocksToRepositoryAsync().ConfigureAwait(false);
            }

            if (this.pendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync().ConfigureAwait(false);
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
