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
    /// return a Next result which cause the <see cref="BlockStoreLoop"/> to execute
    /// the next step <see cref="DownloadBlockStep"/>.
    /// </para>
    /// <para>
    /// If in IBD (Initial Block Download) and batch count is not yet reached,
    /// return a Break result causing the <see cref="BlockStoreLoop"/> to break out of the while loop
    /// and start again.
    /// </para>
    /// <para>
    /// Loop over the pending blocks and push to the repository in batches.
    /// if a stop condition is met break from the inner loop and return a Continue() result.
    /// This will cause the <see cref="BlockStoreLoop"/> to skip over <see cref="DownloadBlockStep"/> and start
    /// the loop again.
    /// </para>
    /// </summary>
    internal sealed class ProcessPendingStorageStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The block currently being processed.
        /// </summary>
        internal ChainedBlock NextChainedBlock { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Used to check if we should break execution when the next block's previous hash doesn't
        /// match this block's hash.
        /// </summary>
        internal ChainedBlock PreviousChainedBlock { get; private set; }

        /// <summary>
        /// If this value reaches <see cref="BlockStoreLoop.MaxPendingInsertBlockSize"/> the step will exit./>
        /// </summary>
        internal int PendingStorageBatchSize = 0;

        /// <summary>
        /// The last item that was dequeued from <see cref="PendingBlockPairsToStore"/>.
        /// </summary>
        internal BlockPair PendingBlockPairToStore;

        /// <summary>
        /// A collection of blocks that are pending to be pushed to store.
        /// </summary>
        internal ConcurrentStack<BlockPair> PendingBlockPairsToStore = new ConcurrentStack<BlockPair>();


        internal ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            this.NextChainedBlock = nextChainedBlock;
            this.CancellationToken = cancellationToken;
            
            // Next block does not exist in pending storage, continue onto the download blocks step.
            if (!this.BlockStoreLoop.PendingStorage.ContainsKey(this.NextChainedBlock.HashBlock))
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

            if (this.BlockStoreLoop.InitialBlockDownloadState.IsInitialBlockDownload())
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
        /// When the node disposes, process all the blocks in <see cref="BlockStoreLoop.PendingStorage"/> until
        /// its empty
        /// </summary>
        private async Task<StepResult> ProcessWhenDisposingAsync()
        {
            while (this.BlockStoreLoop.PendingStorage.Count > 0)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;
            }

            if (this.PendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync();

            this.logger.LogTrace("(-):{0}", StepResult.Stop);
            return StepResult.Stop;
        }

        /// <summary>
        /// When the node is in IBD wait for <see cref="BlockStoreLoop.PendingStorageBatchThreshold"/> to be true then continuously process all the blocks in <see cref="BlockStoreLoop.PendingStorage"/> until
        /// a stop condition is found, the blocks will be pushed to the repository in batches of size <see cref="BlockStoreLoop.MaxPendingInsertBlockSize"/>.
        /// </summary>
        private async Task<StepResult> ProcessWhenInIBDAsync()
        {
            if (this.BlockStoreLoop.PendingStorage.Count < BlockStoreLoop.PendingStorageBatchThreshold)
                return StepResult.Continue;

            while (this.CancellationToken.IsCancellationRequested == false)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;

                if (this.PendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize)
                    await this.PushBlocksToRepositoryAsync();
            }

            if (this.PendingBlockPairsToStore.Any())
                await this.PushBlocksToRepositoryAsync();

            this.logger.LogTrace("(-):{0}", StepResult.Continue);
            return StepResult.Continue;
        }

        /// <summary>
        /// When the node is NOT in IBD, process and push the blocks in <see cref="BlockStoreLoop.PendingStorage"/> immediately
        /// to the block repository without checking batch size.
        /// </summary>
        private async Task<StepResult> ProcessWhenNotInIBDAsync()
        {
            while (this.CancellationToken.IsCancellationRequested == false)
            {
                StepResult result = this.PrepareNextBlockFromPendingStorage();
                if (result == StepResult.Stop)
                    break;
            }

            if (this.PendingBlockPairsToStore.Any())
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
            var blockIsInPendingStorage = this.BlockStoreLoop.PendingStorage.TryRemove(this.NextChainedBlock.HashBlock, out this.PendingBlockPairToStore);
            if (blockIsInPendingStorage)
            {
                this.PendingBlockPairsToStore.Push(this.PendingBlockPairToStore);
                this.PendingStorageBatchSize += this.PendingBlockPairToStore.Block.GetSerializedSize();
            }

            return this.CanProcessNextBlock() ? StepResult.Next : StepResult.Stop;
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks and set the Store's tip to <see cref="NextChainedBlock"/>
        /// </summary>
        private async Task PushBlocksToRepositoryAsync()
        {
            await this.BlockStoreLoop.BlockRepository.PutAsync(this.PendingBlockPairsToStore.First().ChainedBlock.HashBlock, this.PendingBlockPairsToStore.Select(b => b.Block).ToList());
            this.BlockStoreLoop.SetStoreTip(this.PendingBlockPairsToStore.First().ChainedBlock);

            this.PendingBlockPairToStore = null;
            this.PendingBlockPairsToStore.Clear();
            this.PendingStorageBatchSize = 0;
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
        internal bool CanProcessNextBlock()
        {
            this.logger.LogTrace("()");

            this.PreviousChainedBlock = this.NextChainedBlock;
            this.NextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(this.NextChainedBlock.Height + 1);

            if (this.NextChainedBlock == null)
            {
                this.logger.LogTrace("(-)[NO_NEXT]:false");
                return false;
            }

            if (this.NextChainedBlock.Header.HashPrevBlock != this.PreviousChainedBlock.HashBlock)
            {
                this.logger.LogTrace("(-)[REORG]:false");
                return false;
            }

            if (this.NextChainedBlock.Height > this.BlockStoreLoop.ChainState.ConsensusTip?.Height)
            {
                this.logger.LogTrace("(-)[NEXT_GT_CONSENSUS_TIP]:false");
                return false;
            }

            this.logger.LogTrace("(-):true");
            return true;
        }
    }
}
