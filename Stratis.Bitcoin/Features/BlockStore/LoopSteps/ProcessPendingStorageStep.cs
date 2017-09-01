using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
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

            BlockPair pendingBlockPairToStore;
            if (!this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out pendingBlockPairToStore))
                return StepResult.Next;

            var pendingBlockPairsToStore = new List<BlockPair>();
            pendingBlockPairsToStore.Add(pendingBlockPairToStore);
            int pendingStorageBatchSize = pendingBlockPairToStore.Block.GetSerializedSize();

            ChainedBlock lastFoundChainedBlock = nextChainedBlock;

            while (!cancellationToken.IsCancellationRequested)
            {
                ChainedBlock inputChainedBlock = nextChainedBlock;
                nextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

                bool breakExecution = this.ShouldBreakExecution(inputChainedBlock, nextChainedBlock);
                if (!breakExecution && !this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out pendingBlockPairToStore))
                    breakExecution = true;

                if (breakExecution)
                {
                    if (!pendingBlockPairsToStore.Any())
                        break;
                }
                else
                {
                    pendingBlockPairsToStore.Add(pendingBlockPairToStore);
                    pendingStorageBatchSize += pendingBlockPairToStore.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler	
                    lastFoundChainedBlock = nextChainedBlock;
                }

                if ((pendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize) || breakExecution)
                {
                    StepResult result = await this.PushPendingBlocksToRepository(pendingStorageBatchSize, pendingBlockPairsToStore, lastFoundChainedBlock, cancellationToken, breakExecution);
                    if (result == StepResult.Stop)
                        break;

                    pendingBlockPairsToStore.Clear();
                    pendingStorageBatchSize = 0;

                    if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload) // this can be tweaked if insert is effecting the consensus speed
                        await Task.Delay(this.BlockStoreLoop.PushIntervalIBD, cancellationToken);
                }
            }

            return StepResult.Continue;
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks
        /// Set the Store's tip to <see cref="lastFoundChainedBlock"/>
        /// </summary>
        private async Task<StepResult> PushPendingBlocksToRepository(int pendingStorageBatchSize, List<BlockPair> pendingBlockPairsToStore, ChainedBlock lastFoundChainedBlock, CancellationToken cancellationToken, bool breakExecution)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:'{6}/{7}',{8}:{9})", nameof(pendingStorageBatchSize), pendingStorageBatchSize, nameof(pendingBlockPairsToStore), nameof(pendingBlockPairsToStore.Count), pendingBlockPairsToStore?.Count, nameof(lastFoundChainedBlock), lastFoundChainedBlock?.HashBlock, lastFoundChainedBlock?.Height, nameof(breakExecution), breakExecution);

            await this.BlockStoreLoop.BlockRepository.PutAsync(lastFoundChainedBlock.HashBlock, pendingBlockPairsToStore.Select(b => b.Block).ToList());

            this.BlockStoreLoop.SetStoreTip(lastFoundChainedBlock);

            if (breakExecution)
                return StepResult.Stop;

            return StepResult.Next;
        }

        /// <summary>
        /// Break execution if:
        /// <list>
        ///     <item>1: At the tip</item>
        ///     <item>2: Block is already in store or pending insertion</item>
        /// </list>
        /// </summary>
        private bool ShouldBreakExecution(ChainedBlock inputChainedBlock, ChainedBlock nextChainedBlock)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:'{4}/{5}')", nameof(inputChainedBlock), inputChainedBlock?.HashBlock, inputChainedBlock?.Height, nameof(nextChainedBlock), nextChainedBlock?.HashBlock, nextChainedBlock?.Height);

            bool res = (nextChainedBlock == null)
                || (nextChainedBlock.Header.HashPrevBlock != inputChainedBlock.HashBlock)
                || (nextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height);

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
    }
}