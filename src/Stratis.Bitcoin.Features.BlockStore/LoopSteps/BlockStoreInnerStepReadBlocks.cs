using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    using static BlockPuller;

    /// <summary>
    /// Reads blocks from the <see cref="BlockPuller"/> in a loop and removes block
    /// from the <see cref="BlockStoreInnerStepContext.DownloadStack"/>.
    /// <para>
    /// If the block exists in the puller add the the downloaded block to the store to
    /// push to the repository. If <see cref="BlockStoreInnerStepReadBlocks.ShouldBlocksBePushedToRepository"/> returns
    /// true, push the blocks in the <see cref="BlockStoreInnerStepContext.Store"/> to the block repository.
    /// </para>
    /// <para>
    /// When the download stack is empty return a <see cref="InnerStepResult.Stop"/> result causing the <see cref="BlockStoreLoop"/> to
    /// start again.
    /// </para>
    /// <para>
    /// If a block is stalled or lost to the downloader, start again after a threshold <see cref="BlockStoreLoop.StallCount"/>
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepReadBlocks : BlockStoreInnerStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public BlockStoreInnerStepReadBlocks(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            if (!context.DownloadStack.Any())
            {
                this.logger.LogTrace("(-)[EMPTY_STACK1]:{0}", InnerStepResult.Stop);
                return InnerStepResult.Stop;
            }

            while (context.BlocksPushedCount <= BlockStoreInnerStepContext.DownloadStackPushThreshold)
            {
                DownloadedBlock downloadedBlock;
                ChainedBlock nextBlock = context.DownloadStack.Peek();

                if (context.BlockStoreLoop.BlockPuller.TryGetBlock(nextBlock, out downloadedBlock))
                {
                    this.logger.LogTrace("Puller provided block '{0}', length {1}.", nextBlock, downloadedBlock.Length);

                    ChainedBlock lastBlockToPush = this.AddDownloadedBlockToStore(context, downloadedBlock);

                    if (this.ShouldBlocksBePushedToRepository(context))
                    {
                        await this.PushBlocksToRepositoryAsync(context, lastBlockToPush);

                        if (!context.DownloadStack.Any())
                        {
                            this.logger.LogTrace("(-)[EMPTY_STACK2]:{0}", InnerStepResult.Stop);
                            return InnerStepResult.Stop;
                        }
                    }
                }
                else
                {
                    if (context.StallCount > context.StallCountThreshold)
                    {
                        // Increase limit by 10 % to allow adjustments for low speed connections.
                        // Eventually, the limit be high enough to allow normal operation.
                        context.StallCountThreshold += context.StallCountThreshold / 10;
                        this.logger.LogTrace("Stall count threshold increased to {0}.", context.StallCountThreshold);
                        this.logger.LogTrace("(-)[STALLING]:{0}", InnerStepResult.Stop);
                        return InnerStepResult.Stop;
                    }

                    this.logger.LogTrace("Block '{0}' not available, stall count is {1}, waiting {2} ms...", nextBlock, context.StallCount, BlockStoreInnerStepContext.StallDelayMs);
                    await Task.Delay(BlockStoreInnerStepContext.StallDelayMs, context.CancellationToken);

                    context.StallCount++;
                }
            }

            context.BlocksPushedCount = 0;

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);
            return InnerStepResult.Next;
        }

        /// <summary> Adds the downloaded block to the store and resets the stall count.</summary>
        private ChainedBlock AddDownloadedBlockToStore(BlockStoreInnerStepContext context, DownloadedBlock downloadedBlock)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(downloadedBlock), nameof(downloadedBlock.Length), downloadedBlock.Length);

            ChainedBlock chainedBlockToStore = context.DownloadStack.Dequeue();
            context.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToStore));

            context.InsertBlockSize += downloadedBlock.Length;
            context.StallCount = 0;

            this.logger.LogTrace("(-):'{0}'", chainedBlockToStore);
            return chainedBlockToStore;
        }

        /// <summary> Determines whether or not its time for <see cref="BlockStoreInnerStepReadBlocks"/>
        /// to push (persist) the downloaded blocks to the repository.</summary>
        private bool ShouldBlocksBePushedToRepository(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            DateTime now = context.DateTimeProvider.GetUtcNow();
            uint lastFlushDiff = (uint)(now - context.LastDownloadStackFlushTime).TotalMilliseconds;

            bool pushBufferSizeReached = context.InsertBlockSize > BlockStoreLoop.MaxInsertBlockSize;
            bool downloadStackEmpty = !context.DownloadStack.Any();
            bool pushTimeReached = lastFlushDiff > BlockStoreInnerStepContext.MaxDownloadStackFlushTimeMs;

            this.logger.LogTrace("Insert block size is {0} bytes{1}, download stack contains {2} blocks, last flush time was {3} ms ago{4}.",
                context.InsertBlockSize, pushBufferSizeReached ? " (threshold reached)" : "", context.DownloadStack.Count, lastFlushDiff, pushTimeReached ? " (threshold reached)" : "");

            bool res = pushBufferSizeReached || downloadStackEmpty || pushTimeReached;
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        /// <summary>
        /// Push (persist) the downloaded blocks to the block repository
        /// </summary>
        /// <param name="lastDownloadedBlock">Last block in the list to store, also used to set the store tip.</param>
        private async Task PushBlocksToRepositoryAsync(BlockStoreInnerStepContext context, ChainedBlock lastDownloadedBlock)
        {
            this.logger.LogTrace("()");

            List<Block> blocksToStore = context.Store.Select(bp => bp.Block).ToList();
            await context.BlockStoreLoop.BlockRepository.PutAsync(lastDownloadedBlock.HashBlock, blocksToStore);
            context.BlocksPushedCount += blocksToStore.Count;
            this.logger.LogTrace("{0} blocks pushed to the repository, {1} blocks pushed in total.", blocksToStore.Count, context.BlocksPushedCount);

            context.BlockStoreLoop.SetStoreTip(lastDownloadedBlock);
            context.InsertBlockSize = 0;
            context.LastDownloadStackFlushTime = context.DateTimeProvider.GetUtcNow();

            context.Store.Clear();

            this.logger.LogTrace("(-)");
        }
    }
}