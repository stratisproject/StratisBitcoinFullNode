using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        internal ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(nextChainedBlock), nextChainedBlock.HashBlock, nextChainedBlock.Height, nameof(disposeMode), disposeMode);

            var context = new ProcessPendingStorageContext(this.BlockStoreLoop, nextChainedBlock);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!this.BlockStoreLoop.PendingStorage.TryRemove(context.NextChainedBlock.HashBlock, out context.PendingBlockPairToStore))
                    return StepResult.Next;

                context.PendingBlockPairsToStore.Push(context.PendingBlockPairToStore);

                if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload == false)
                    break;

                context.PendingStorageBatchSize += context.PendingBlockPairToStore.Block.GetSerializedSize();
                if (context.PendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize)
                {
                    this.logger.LogTrace("{0}:{1} [maxpendinginsertblocksize_reached]", nameof(context.PendingStorageBatchSize), context.PendingStorageBatchSize);
                    break;
                }

                context.GetNextBlock();

                if (ShouldBreakExecution(context))
                    break;
            }

            await PushPendingBlocksToRepository(context);

            return StepResult.Continue;
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks
        /// Set the Store's tip to <see cref="ProcessPendingStorageContext.NextChainedBlock"/>
        /// </summary>
        private async Task PushPendingBlocksToRepository(ProcessPendingStorageContext context)
        {
            await this.BlockStoreLoop.BlockRepository.PutAsync(context.PendingBlockPairsToStore.First().ChainedBlock.HashBlock, context.PendingBlockPairsToStore.Select(b => b.Block).ToList());
            this.BlockStoreLoop.SetStoreTip(context.PendingBlockPairsToStore.First().ChainedBlock);

            this.logger.LogTrace("({0}.{1}:{2} pushed to the repository.')", nameof(context.PendingBlockPairsToStore), nameof(context.PendingBlockPairsToStore.Count), context.PendingBlockPairsToStore?.Count);
        }

        /// <summary>
        /// Break execution if:
        /// <list>
        ///     <item>1: At the tip</item>
        ///     <item>2: Block is already in store or pending insertion</item>
        /// </list>
        /// </summary>
        private bool ShouldBreakExecution(ProcessPendingStorageContext context)
        {
            if (context.NextChainedBlock == null)
            {
                this.logger.LogTrace("{0}:'{1}/{2}' [nextchainedblock_null]", nameof(context.NextChainedBlock), context.NextChainedBlock?.HashBlock, context.NextChainedBlock?.Height);
                return true;
            }

            if ((context.InputChainedBlock != null && (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock)))
            {
                this.logger.LogTrace("{0}:'{1}/{2}' [invalid_previous_block]", nameof(context.NextChainedBlock.Header.HashPrevBlock), context.NextChainedBlock.Header.HashPrevBlock, context.InputChainedBlock.HashBlock);
                return true;
            }

            if (context.NextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
            {
                this.logger.LogTrace("{0}:{1} / {2}:{3}' [store_at_tip]", nameof(context.NextChainedBlock), context.NextChainedBlock.Height, nameof(this.BlockStoreLoop.ChainState.HighestValidatedPoW), this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height);
                return true;
            }

            return false;
        }
    }


    /// <summary>
    /// Context class that's used by <see cref="ProcessPendingStorageStep"/> 
    /// </summary>
    internal sealed class ProcessPendingStorageContext
    {
        internal ProcessPendingStorageContext(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock)
        {
            this.BlockStoreLoop = blockStoreLoop;
            this.NextChainedBlock = nextChainedBlock;
        }

        internal BlockStoreLoop BlockStoreLoop { get; private set; }

        /// <summary>
        /// Used to check if we should break execution when the next block's previous has doesn't
        /// match this block's hash.
        /// </summary>
        internal ChainedBlock InputChainedBlock { get; private set; }

        /// <summary>
        /// The block currently being processed.
        /// </summary>
        internal ChainedBlock NextChainedBlock { get; private set; }

        /// <summary>
        /// If this value reaches <see cref="BlockStoreLoop.MaxPendingInsertBlockSize" the step will exit./>
        /// </summary>
        internal int PendingStorageBatchSize = 0;

        internal BlockPair PendingBlockPairToStore;
        internal ConcurrentStack<BlockPair> PendingBlockPairsToStore = new ConcurrentStack<BlockPair>();

        /// <summary>
        /// Gets the next block to process.
        /// </summary>
        internal void GetNextBlock()
        {
            this.InputChainedBlock = this.NextChainedBlock;
            this.NextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(this.NextChainedBlock.Height + 1);
        }
    }
}