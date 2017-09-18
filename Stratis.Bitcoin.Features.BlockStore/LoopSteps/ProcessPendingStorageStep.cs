﻿using Microsoft.Extensions.Logging;
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
            this.logger.LogTrace("{0}:'{1}/{2}',{3}:{4}", nameof(nextChainedBlock), nextChainedBlock?.HashBlock, nextChainedBlock?.Height, nameof(disposeMode), disposeMode);

            var context = new ProcessPendingStorageContext(this.BlockStoreLoop, nextChainedBlock, cancellationToken);

            // Next block does not exist in pending storage, continue onto the download blocks step.
            if (!this.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
                return StepResult.Next;

            if (disposeMode)
                return await ProcessWhenDisposing(context);

            if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload)
                return await ProcessWhenInIBD(context);

            if (!this.BlockStoreLoop.ChainState.IsInitialBlockDownload)
                return await ProcessWhenNotInIBD(context);

            return StepResult.Stop;
        }

        /// <summary>
        /// When the node disposes, process all the blocks in <see cref="BlockStoreLoop.PendingStorage"/> until 
        /// its empty
        /// </summary>
        private async Task<StepResult> ProcessWhenDisposing(ProcessPendingStorageContext context)
        {
            this.logger.LogDebug("{0}:'{1}/{2}'", nameof(context.NextChainedBlock), context.NextChainedBlock.HashBlock, context.NextChainedBlock.Height);

            while (this.BlockStoreLoop.PendingStorage.Count > 0)
            {
                PrepareNextBlockFromPendingStorage(context);

                var canProcessNextBlock = context.CanProcessNextBlock();
                if (canProcessNextBlock == false)
                    if (!context.PendingBlockPairsToStore.Any())
                        break;
            }

            if (context.PendingBlockPairsToStore.Any())
                await PushBlocksToRepository(context);

            return StepResult.Stop;
        }

        /// <summary>
        /// When the node is in IBD continuously process all the blocks in <see cref="BlockStoreLoop.PendingStorage"/> until 
        /// a stop condition is found.
        /// </summary>
        private async Task<StepResult> ProcessWhenInIBD(ProcessPendingStorageContext context)
        {
            this.logger.LogDebug("{0}:'{1}/{2}'", nameof(context.NextChainedBlock), context.NextChainedBlock.HashBlock, context.NextChainedBlock.Height);

            if (this.BlockStoreLoop.PendingStorage.Count < BlockStoreLoop.PendingStorageBatchThreshold)
                return StepResult.Continue;

            do
            {
                PrepareNextBlockFromPendingStorage(context);

                var canProcessNextBlock = context.CanProcessNextBlock();
                if (canProcessNextBlock == false)
                {
                    if (context.PendingBlockPairsToStore.Any())
                        await PushBlocksToRepository(context);

                    break;
                }

                if (context.PendingStorageBatchSize > BlockStoreLoop.MaxPendingInsertBlockSize)
                {
                    await PushBlocksToRepository(context);

                    context.PendingBlockPairsToStore.Clear();
                    context.PendingStorageBatchSize = 0;
                }

            } while (context.CancellationToken.IsCancellationRequested == false);

            return StepResult.Continue;
        }

        /// <summary>
        /// When the node is NOT in IBD, process and push the blocks in <see cref="BlockStoreLoop.PendingStorage"/> immediately 
        /// to the block repository without checking batch size.
        /// </summary>
        private async Task<StepResult> ProcessWhenNotInIBD(ProcessPendingStorageContext context)
        {
            this.logger.LogDebug("{0}:'{1}/{2}'", nameof(context.NextChainedBlock), context.NextChainedBlock.HashBlock, context.NextChainedBlock.Height);

            do
            {
                PrepareNextBlockFromPendingStorage(context);

                await PushBlocksToRepository(context);

                var canProcessNextBlock = context.CanProcessNextBlock();
                if (canProcessNextBlock == false)
                    break;

            } while (context.CancellationToken.IsCancellationRequested == false);

            return StepResult.Continue;
        }

        /// <summary>
        /// Tries to get and remove the next block from pending storage. If it exists
        /// then add it to <see cref="ProcessPendingStorageContext.PendingBlockPairsToStore"/>
        /// </summary>
        /// <param name="context"><see cref="ProcessPendingStorageContext"/></param>
        private void PrepareNextBlockFromPendingStorage(ProcessPendingStorageContext context)
        {
            var blockIsInPendingStorage = this.BlockStoreLoop.PendingStorage.TryRemove(context.NextChainedBlock.HashBlock, out context.PendingBlockPairToStore);
            if (blockIsInPendingStorage)
            {
                context.PendingBlockPairsToStore.Push(context.PendingBlockPairToStore);
                context.PendingStorageBatchSize += context.PendingBlockPairToStore.Block.GetSerializedSize();
            }
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks and set the Store's tip to <see cref="ProcessPendingStorageContext.NextChainedBlock"/>
        /// </summary>
        /// <param name="context"><see cref="ProcessPendingStorageContext"/></param>
        private async Task PushBlocksToRepository(ProcessPendingStorageContext context)
        {
            await this.BlockStoreLoop.BlockRepository.PutAsync(context.PendingBlockPairsToStore.First().ChainedBlock.HashBlock, context.PendingBlockPairsToStore.Select(b => b.Block).ToList());

            this.BlockStoreLoop.SetStoreTip(context.PendingBlockPairsToStore.First().ChainedBlock);

            this.logger.LogDebug(context.ToString());
        }
    }

    /// <summary>
    /// Context class thats used by <see cref="ProcessPendingStorageStep"/> 
    /// </summary>
    internal sealed class ProcessPendingStorageContext
    {/// <summary>
     /// 
     /// </summary>

        internal ProcessPendingStorageContext(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken)
        {
            this.BlockStoreLoop = blockStoreLoop;
            this.NextChainedBlock = nextChainedBlock;
            this.CancellationToken = cancellationToken;
        }

        internal BlockStoreLoop BlockStoreLoop { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

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
        /// Break execution if:
        /// <list>
        ///     <item>1: Next block is null.</item>
        ///     <item>2: Next block previous hash does not match previous block.</item>
        ///     <item>3: Netx block is at tip.</item>
        ///     <item>4: Store tip is as consensus.</item>
        /// </list>
        /// </summary>
        /// <returns>Returns <c>true</c> if none of the above condition were met, i.e. the next block can be processed.</returns>
        internal bool CanProcessNextBlock()
        {
            this.InputChainedBlock = this.NextChainedBlock;
            this.NextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(this.NextChainedBlock.Height + 1);

            if (this.NextChainedBlock == null)
                return false;
            if (this.NextChainedBlock.Header.HashPrevBlock != this.InputChainedBlock.HashBlock)
                return false;
            if (this.NextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return false;
            if (this.BlockStoreLoop.StoreTip.Height >= this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return false;

            return true;
        }

        public override string ToString()
        {
            return (string.Format("{0}:{1} / {2}.{3}:{4} / {5}:{6} / {7}:{8}",
                    nameof(this.BlockStoreLoop.ChainState.IsInitialBlockDownload), this.BlockStoreLoop.ChainState.IsInitialBlockDownload,
                    nameof(this.PendingBlockPairsToStore), nameof(this.PendingBlockPairsToStore.Count),
                    this.PendingBlockPairsToStore?.Count, nameof(this.PendingStorageBatchSize), this.PendingStorageBatchSize,
                    nameof(this.BlockStoreLoop.StoreTip), this.BlockStoreLoop.StoreTip));
        }
    }
}