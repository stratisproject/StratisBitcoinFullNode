﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Find blocks to download by asking the BlockPuller.
    /// <para>
    /// Find blocks until <see cref="BlockStoreInnerStepContext.DownloadStack"/> contains 
    /// <see cref="BlockStoreInnerStepContext.DownloadStackThreshold"/> blocks.
    /// </para>
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there are still blocks to download, stop finding new blocks and only execute
    /// the read blocks inner step <see cref="BlockStoreInnerStepReadBlocks"/>.
    /// </para> 
    /// </summary>
    public sealed class BlockStoreInnerStepFindBlocks : BlockStoreInnerStep
    {
        private readonly ILogger logger;

        public BlockStoreInnerStepFindBlocks(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            var batchSize = BlockStoreInnerStepContext.DownloadStackThreshold - context.DownloadStack.Count;
            var batchList = new List<ChainedBlock>(batchSize);

            while (batchList.Count < batchSize)
            {
                if (await ShouldStopFindingBlocks(context))
                {
                    context.StopFindingBlocks();
                    break;
                }

                batchList.Add(context.NextChainedBlock);
                context.DownloadStack.Enqueue(context.NextChainedBlock);
                context.GetNextBlock();
            }


            if (batchList.Any())
            {
                this.logger.LogTrace("{0} blocks requested to be downloaded by the puller.", batchList.Count);
                context.BlockStoreLoop.BlockPuller.AskForMultipleBlocks(batchList.ToArray());
            }

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);

            return InnerStepResult.Next;
        }

        private async Task<bool> ShouldStopFindingBlocks(BlockStoreInnerStepContext context)
        {
            if (context.NextChainedBlock == null)
                return true;

            if (context.InputChainedBlock != null && (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock))
                return true;

            if (context.NextChainedBlock.Height > context.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return true;

            if (context.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
                return true;

            if (await context.BlockStoreLoop.BlockRepository.ExistAsync(context.NextChainedBlock.HashBlock))
                return true;

            return false;
        }
    }
}