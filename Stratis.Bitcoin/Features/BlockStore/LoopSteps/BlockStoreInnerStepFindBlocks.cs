using Microsoft.Extensions.Logging;
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
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public BlockStoreInnerStepFindBlocks(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
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

            return InnerStepResult.Next;
        }

        private async Task<bool> ShouldStopFindingBlocks(BlockStoreInnerStepContext context)
        {
            if (context.NextChainedBlock == null)
            {
                this.logger.LogTrace("{0} is null", nameof(context.NextChainedBlock));
                return true;
            }

            if (context.InputChainedBlock != null && (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock))
            {
                this.logger.LogTrace("{0} != {1}", nameof(context.NextChainedBlock.Header.HashPrevBlock), nameof(context.InputChainedBlock.HashBlock));
                return true;
            }

            if (context.NextChainedBlock.Height > context.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
            {
                this.logger.LogTrace("{0} height > {1} height", nameof(context.NextChainedBlock), nameof(context.BlockStoreLoop.ChainState.HighestValidatedPoW));
                return true;
            }

            if (context.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("{0}='{1}/{2}' exists in pending storage.", nameof(context.NextChainedBlock), context.NextChainedBlock.HashBlock, context.NextChainedBlock.Height);
                return true;
            }

            if (await context.BlockStoreLoop.BlockRepository.ExistAsync(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("{0}='{1}/{2}' exists in the repository.", nameof(context.NextChainedBlock), context.NextChainedBlock.HashBlock, context.NextChainedBlock.Height);
                return true;
            }

            return false;
        }
    }
}