﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using System.Collections.Generic;
using System.Linq; 
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Downloads blocks from the BlockPuller removes block from the DownloadStack.
    /// <para>
    /// If the block exists in the puller add the the downloaded block to the store to
    /// push to the repository. If the <see cref="BlockStoreLoop.InsertBlockSizeThreshold"/> has been reached
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

                this.logger.LogTrace("Insert block size is {0} bytes, download stack contains {1} more blocks to download.", context.InsertBlockSize, context.DownloadStack.Count);
                if (context.InsertBlockSize > context.BlockStoreLoop.InsertBlockSizeThreshold || !context.DownloadStack.Any())
                {
                    List<Block> blocksToStore = context.Store.Select(bp => bp.Block).ToList();
                    await context.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, blocksToStore);
                    context.BlockStoreLoop.SetStoreTip(chainedBlockToDownload);
                    context.InsertBlockSize = 0;
                    context.Store.Clear();

                    if (!context.DownloadStack.Any())
                    {
                        this.logger.LogTrace("(-):{0}", InnerStepResult.Stop);
                        return InnerStepResult.Stop;
                    }
                }
            }
            else
            {
                if (context.StallCount > 10000)
                {
                    this.logger.LogTrace("(-):{0}", InnerStepResult.Stop);
                    return InnerStepResult.Stop;
                }

                this.logger.LogTrace("Block '{0}/{1}' not available, stall count is {2}, waiting 100ms...", nextBlock.HashBlock, nextBlock.Height, context.StallCount);
                await Task.Delay(100, context.CancellationToken);

                context.StallCount++;
            }

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);
            return InnerStepResult.Next;
        }
    }
}