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
    /// Remove the BlockPair from PendingStorage and return for further processing
    /// If the next chained block does not exist in pending storage
    /// return a Next() result which cause the BlockStoreLoop to execute
    /// the next step <see cref="DownloadBlockStep"/>.
    /// </para>
    /// <para>
    /// If in IBD (Initial Block Download) and batch count is not yet reached, 
    /// return a Break() result causing the BlockStoreLoop to break out of the while loop
    /// and start again.
    /// </para>
    /// <para>
    /// Loop over the pending blocks and push to the repository in batches.
    /// if a stop condition is met break from the inner loop and return a Continue() result.
    /// This will cause the BlockStoreLoop to skip over  <see cref="DownloadBlockStep"/> and start
    /// the loop again. 
    /// </para>
    /// </summary>
    internal sealed class ProcessPendingStorageStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private BlockPair pendingBlockPairToStore;

        internal ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(nextChainedBlock), nextChainedBlock.HashBlock, nextChainedBlock.Height, nameof(disposeMode), disposeMode);

            if (!this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
            {
                this.logger.LogTrace("(-):{0}", StepResult.Next);
                return StepResult.Next;
            }

            if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload && !disposeMode)
            {
                if (this.BlockStoreLoop.PendingStorage.Skip(0).Count() < this.BlockStoreLoop.PendingStorageBatchThreshold)
                {
                    this.logger.LogTrace("(-):{0}", StepResult.Stop);
                    return StepResult.Stop;
                }
            }

            var pendingBlockPairsToStore = new List<BlockPair>();
            pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);
            var pendingStorageBatchSize = this.pendingBlockPairToStore.Block.GetSerializedSize();

            var lastFoundChainedBlock = nextChainedBlock;

            while (!cancellationToken.IsCancellationRequested)
            {
                var inputChainedBlock = nextChainedBlock;
                nextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

                var breakExecution = ShouldBreakExecution(inputChainedBlock, nextChainedBlock);
                if (!breakExecution && !this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
                    breakExecution = true;

                if (breakExecution)
                {
                    if (!pendingBlockPairsToStore.Any())
                        break;
                }
                else
                {
                    pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);
                    pendingStorageBatchSize += this.pendingBlockPairToStore.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler	
                    lastFoundChainedBlock = nextChainedBlock;
                }

                if (pendingStorageBatchSize > this.BlockStoreLoop.InsertBlockSizeThreshold || breakExecution)
                {
                    var result = await PushPendingBlocksToRepository(pendingStorageBatchSize, pendingBlockPairsToStore, lastFoundChainedBlock, cancellationToken, breakExecution);
                    if (result == StepResult.Stop)
                        break;

                    pendingBlockPairsToStore.Clear();
                    pendingStorageBatchSize = 0;

                    if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload) // this can be tweaked if insert is effecting the consensus speed
                        await Task.Delay(this.BlockStoreLoop.PushIntervalIBD, cancellationToken);
                }
            }

            pendingBlockPairsToStore.Clear();
            pendingBlockPairsToStore = null;

            this.pendingBlockPairToStore = null;

            this.logger.LogTrace("(-):{0}", StepResult.Continue);
            return StepResult.Continue;
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks
        /// Set the Store's tip to <see cref="lastFoundChainedBlock"/>
        /// </summary>
        private async Task<StepResult> PushPendingBlocksToRepository(int pendingStorageBatchSize, List<BlockPair> pendingBlockPairsToStore, ChainedBlock lastFoundChainedBlock, CancellationToken cancellationToken, bool breakExecution)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:'{6}/{7}',{8}:{9})", nameof(pendingBlockPairsToStore), pendingBlockPairsToStore, nameof(pendingBlockPairsToStore), nameof(pendingBlockPairsToStore.Count), pendingBlockPairsToStore?.Count, nameof(lastFoundChainedBlock), lastFoundChainedBlock?.HashBlock, lastFoundChainedBlock?.Height, nameof(breakExecution), breakExecution);

            await this.BlockStoreLoop.BlockRepository.PutAsync(lastFoundChainedBlock.HashBlock, pendingBlockPairsToStore.Select(b => b.Block).ToList());

            this.BlockStoreLoop.SetStoreTip(lastFoundChainedBlock);

            if (breakExecution)
            {
                this.logger.LogTrace("(-):{0}", StepResult.Stop);
                return StepResult.Stop;
            }

            this.logger.LogTrace("(-):{0}", StepResult.Next);
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