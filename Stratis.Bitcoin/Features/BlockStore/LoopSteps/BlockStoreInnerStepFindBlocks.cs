﻿using Microsoft.Extensions.Logging;
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
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public BlockStoreInnerStepFindBlocks(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            while (await ShouldStopFindingBlocks(context) == false)
            {
                context.DownloadStack.Enqueue(context.NextChainedBlock);
                context.GetNextBlock();
            }

            context.StopFindingBlocks();

            if (context.DownloadStack.Any())
                context.BlockStoreLoop.BlockPuller.AskForMultipleBlocks(context.DownloadStack.ToArray());

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);

            return InnerStepResult.Next;
        }

        private async Task<bool> ShouldStopFindingBlocks(BlockStoreInnerStepContext context)
        {
            if (context.NextChainedBlock == null)
                return true;

            if (context.DownloadStack.Count >= BlockStoreInnerStepContext.DownloadStackThreshold)
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