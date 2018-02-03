using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Check if the next block is in pending storage i.e. first process pending storage blocks
    /// before find and downloading more blocks.
    /// <para>
    /// Remove the BlockPair from PendingStorage and return for further processing.
    /// If the next chained block does not exist in pending storage
    /// return a Next result which cause the <see cref="blockStoreLoop"/> to execute
    /// the next step <see cref="DownloadBlockStep"/>.
    /// </para>
    /// <para>
    /// If in IBD (Initial Block Download) and batch count is not yet reached,
    /// return a Break result causing the <see cref="blockStoreLoop"/> to break out of the while loop
    /// and start again.
    /// </para>
    /// <para>
    /// Loop over the pending blocks and push to the repository in batches.
    /// if a stop condition is met break from the inner loop and return a Continue() result.
    /// This will cause the <see cref="blockStoreLoop"/> to skip over <see cref="DownloadBlockStep"/> and start
    /// the loop again.
    /// </para>
    /// </summary>
    internal sealed class ProcessPendingStorageStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The block currently being processed.
        /// </summary>
        private ChainedBlock nextChainedBlock;

        private CancellationToken cancellationToken;

        /// <summary>
        /// Used to check if we should break execution when the next block's previous hash doesn't
        /// match this block's hash.
        /// </summary>
        private ChainedBlock previousChainedBlock;

        /// <summary>
        /// If this value reaches <see cref="blockStoreLoop.MaxPendingInsertBlockSize"/> the step will exit./>
        /// </summary>
        private int pendingStorageBatchSize = 0;

        /// <summary>
        /// The last item that was dequeued from <see cref="pendingBlockPairsToStore"/>.
        /// </summary>
        private BlockPair pendingBlockPairToStore;

        /// <summary>
        /// A collection of blocks that are pending to be pushed to store.
        /// </summary>
        private ConcurrentStack<BlockPair> pendingBlockPairsToStore = new ConcurrentStack<BlockPair>();

        private readonly BlockStoreLoop blockStoreLoop;

        public ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
        {
            this.blockStoreLoop = blockStoreLoop;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }
        
        public async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            this.nextChainedBlock = nextChainedBlock;
            this.cancellationToken = cancellationToken;
            
            // Next block does not exist in pending storage, continue onto the download blocks step.
            if (!this.blockStoreLoop.PendingStorage.ContainsKey(this.nextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-)[NOT_FOUND]:{0}", StepResult.Next);
                return StepResult.Next;
            }

            if (disposeMode)
            {
                StepResult lres = await this.ProcessWhenDisposingAsync();
                this.logger.LogTrace("(-)[DISPOSE]:{0}", lres);
                return lres;
            }

            if (this.blockStoreLoop.InitialBlockDownloadState.IsInitialBlockDownload())
            {
                StepResult lres = await this.ProcessWhenInIBDAsync();
                this.logger.LogTrace("(-)[IBD]:{0}", lres);
                return lres;
            }

            StepResult res = await this.ProcessWhenNotInIBDAsync();
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <summary>
        /// When the node disposes, process all the blocks in <see cref="blockStoreLoop.PendingStorage"/> until
        /// its empty
        /// </summary>
        private async Task<StepResult> ProcessWhenDisposingAsync()
        {
            while (this.blockStoreLoop.PendingStorage.Count > 0)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;
            }

            if (this.pendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync();

            this.logger.LogTrace("(-):{0}", StepResult.Stop);
            return StepResult.Stop;
        }

        /// <summary>
        /// When the node is in IBD wait for <see cref="blockStoreLoop.PendingStorageBatchThreshold"/> to be true then continuously process all the blocks in <see cref="blockStoreLoop.PendingStorage"/> until
        /// a stop condition is found, the blocks will be pushed to the repository in batches of size <see cref="blockStoreLoop.MaxPendingInsertBlockSize"/>.
        /// </summary>
        private async Task<StepResult> ProcessWhenInIBDAsync()
        {
            if (this.blockStoreLoop.PendingStorage.Count < BlockStoreLoop.PendingStorageBatchThreshold)
                return StepResult.Continue;

            while (this.cancellationToken.IsCancellationRequested == false)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;

                if (this.pendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize)
                    await this.PushBlocksToRepositoryAsync();
            }

            if (this.pendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync();

            this.logger.LogTrace("(-):{0}", StepResult.Continue);
            return StepResult.Continue;
        }

        /// <summary>
        /// When the node is NOT in IBD, process and push the blocks in <see cref="blockStoreLoop.PendingStorage"/> immediately
        /// to the block repository without checking batch size.
        /// </summary>
        private async Task<StepResult> ProcessWhenNotInIBDAsync()
        {
            while (this.cancellationToken.IsCancellationRequested == false)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;
            }

            if (this.pendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync();

            this.logger.LogTrace("(-):{0}", StepResult.Continue);
            return StepResult.Continue;
        }

        /// <summary>
        /// Tries to get and remove the next block from pending storage. If it exists
        /// then add it to <see cref="ProcessPendingStoragethis.PendingBlockPairsToStore"/>.
        /// This will also check if the next block can be processed.
        /// </summary>
        private StepResult PrepareNextBlockFromPendingStorage()
        {
            bool blockIsInPendingStorage = this.blockStoreLoop.PendingStorage.TryRemove(this.nextChainedBlock.HashBlock, out this.pendingBlockPairToStore);
            if (blockIsInPendingStorage)
            {
                this.pendingBlockPairsToStore.Push(this.pendingBlockPairToStore);
                this.pendingStorageBatchSize += this.pendingBlockPairToStore.Block.GetSerializedSize();
            }

            return this.CanProcessNextBlock() ? StepResult.Next : StepResult.Stop;
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
