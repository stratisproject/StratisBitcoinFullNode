using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Reads blocks from the <see cref="BlockPuller"/> in a loop and removes block 
    /// from the <see cref="BlockStoreInnerStepContext.DownloadStack"/>.
    /// <para>
    /// If the block exists in the puller add the the downloaded block to the store to
    /// push to the repository. If <see cref="BlockStoreInnerStepContext.ShouldBlocksBePushedToRepository"/> returns
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
                this.logger.LogTrace("(-):{0} empty.", nameof(context.DownloadStack));
                return InnerStepResult.Stop;
            }

            while (context.BlocksPushedCount <= BlockStoreInnerStepContext.DownloadStackPushThreshold)
            {
                DownloadedBlock downloadedBlock;
                ChainedBlock nextBlock = context.DownloadStack.Peek();

                if (context.BlockStoreLoop.BlockPuller.TryGetBlock(nextBlock, out downloadedBlock))
                {
                    this.logger.LogTrace("Puller provided block '{0}/{1}', length {2}.", nextBlock.HashBlock, nextBlock.Height, downloadedBlock.Length);

                    ChainedBlock lastBlockToPush = AddDownloadedBlockToStore(context, downloadedBlock);

                    if (ShouldBlocksBePushedToRepository(context))
                    {
                        await PushBlocksToRepository(context, lastBlockToPush);

                        if (!context.DownloadStack.Any())
                        {
                            this.logger.LogTrace("(-):{0} empty.", nameof(context.DownloadStack));
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
                        this.logger.LogTrace("(-):{0},{1}={2}", InnerStepResult.Stop, nameof(context.StallCountThreshold), context.StallCountThreshold);
                        return InnerStepResult.Stop;
                    }

                    this.logger.LogTrace("Block '{0}/{1}' not available, stall count is {2}, waiting {3} ms...", nextBlock.HashBlock, nextBlock.Height, context.StallCount, BlockStoreInnerStepContext.StallDelayMs);
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
            ChainedBlock chainedBlockToStore = context.DownloadStack.Dequeue();
            context.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToStore));

            context.InsertBlockSize += downloadedBlock.Length;
            context.StallCount = 0;

            this.logger.LogTrace("{0}='{1}/{2}' added to the store.", nameof(chainedBlockToStore), chainedBlockToStore.HashBlock, chainedBlockToStore.Height, context.BlocksPushedCount);
            return chainedBlockToStore;
        }

        /// <summary> Determines whether or not its time for <see cref="BlockStoreInnerStepReadBlocks"/> 
        /// to push (persist) the downloaded blocks to the repository.</summary>
        private bool ShouldBlocksBePushedToRepository(BlockStoreInnerStepContext context)
        {
            var now = context.DateTimeProvider.GetUtcNow();
            uint lastFlushDiff = (uint)(now - context.LastDownloadStackFlushTime).TotalMilliseconds;

            this.logger.LogTrace("Insert block size is {0} bytes, download stack contains {1} more blocks to download, last flush time was {2} ms ago.", context.InsertBlockSize, context.DownloadStack.Count, lastFlushDiff);

            var pushBufferSizeReached = context.InsertBlockSize > BlockStoreLoop.MaxInsertBlockSize;
            var downloadStackEmpty = !context.DownloadStack.Any();
            var pushTimeReached = lastFlushDiff > BlockStoreInnerStepContext.MaxDownloadStackFlushTimeMs;

            this.logger.LogTrace("{0}={1} / {2}={3} / {4}={5}", nameof(pushBufferSizeReached), pushBufferSizeReached, nameof(downloadStackEmpty), downloadStackEmpty, nameof(pushTimeReached), pushTimeReached);
            return pushBufferSizeReached || downloadStackEmpty || pushTimeReached;
        }

        /// <summary>
        /// Push (persist) the downloaded blocks to the block repository
        /// </summary>
        /// <param name="lastDownloadedBlock">Last block in the list to store, also used to set the store tip.</param>
        private async Task PushBlocksToRepository(BlockStoreInnerStepContext context, ChainedBlock lastDownloadedBlock)
        {
            var blocksToStore = context.Store.Select(bp => bp.Block).ToList();
            await context.BlockStoreLoop.BlockRepository.PutAsync(lastDownloadedBlock.HashBlock, blocksToStore);
            context.BlocksPushedCount += blocksToStore.Count;
            this.logger.LogTrace("{0} blocks pushed to the repository. {1}={2}", blocksToStore.Count, nameof(context.BlocksPushedCount), context.BlocksPushedCount);

            context.BlockStoreLoop.SetStoreTip(lastDownloadedBlock);
            context.InsertBlockSize = 0;
            context.LastDownloadStackFlushTime = context.DateTimeProvider.GetUtcNow();

            context.Store.Clear();
        }
    }
}