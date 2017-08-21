using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using System;
using System.Collections.Generic;
using System.Linq; 
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Downloads blocks from the BlockPuller removes block from the DownloadStack.
    /// <para>
    /// If the block exists in the puller add the the downloaded block to the store to
    /// push to the repository. If the <see cref="BlockStoreLoop.MaxInsertBlockSize"/> has been reached
    /// push the blocks in the context's Store to the repository.
    /// </para> 
    /// <para>
    /// When the download stack is empty return a Break() result causing the BlockStoreLoop to
    /// start again.
    /// </para>
    /// <para>
    /// If a block is stalled or lost to the downloader, start again after a threshold <see cref="BlockStoreLoop.StallCount"/>
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepDownloadBlocks : BlockStoreInnerStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public BlockStoreInnerStepDownloadBlocks(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            BlockPuller.DownloadedBlock downloadedBlock;

            ChainedBlock nextBlock = context.DownloadStack.Peek();
            if (context.BlockStoreLoop.BlockPuller.TryGetBlock(nextBlock, out downloadedBlock))
            {
                this.logger.LogTrace("Puller provided block '{0}/{1}', length {2}.", nextBlock.HashBlock, nextBlock.Height, downloadedBlock.Length);
                ChainedBlock chainedBlockToDownload = context.DownloadStack.Dequeue();
                context.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));
                context.InsertBlockSize += downloadedBlock.Length;
                context.StallCount = 0;

                DateTime now = context.DateTimeProvider.GetUtcNow();
                uint lastFlushDiff = (uint)(now - context.LastDownloadStackFlushTime).TotalMilliseconds;
                this.logger.LogTrace("Insert block size is {0} bytes, download stack contains {1} more blocks to download, last flush time was {2} ms ago.", context.InsertBlockSize, context.DownloadStack.Count, lastFlushDiff);

                bool flushBufferSizeReached = context.InsertBlockSize > BlockStoreLoop.MaxInsertBlockSize;
                bool downloadStackEmpty = !context.DownloadStack.Any();
                bool flushTimeReached = lastFlushDiff > BlockStoreInnerStepContext.MaxDownloadStackFlushTimeMs;

                if (flushBufferSizeReached || flushTimeReached || downloadStackEmpty)
                {
                    List<Block> blocksToStore = context.Store.Select(bp => bp.Block).ToList();
                    await context.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, blocksToStore);
                    context.BlockStoreLoop.SetStoreTip(chainedBlockToDownload);
                    context.InsertBlockSize = 0;
                    context.Store.Clear();
                    context.LastDownloadStackFlushTime = context.DateTimeProvider.GetUtcNow();

                    if (!context.DownloadStack.Any())
                    {
                        this.logger.LogTrace("(-):{0}", InnerStepResult.Stop);
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

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);
            return InnerStepResult.Next;
        }
    }
}