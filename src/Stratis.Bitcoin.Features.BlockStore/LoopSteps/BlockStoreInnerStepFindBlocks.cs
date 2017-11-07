using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Find blocks to download by asking the BlockPuller.
    /// <para>
    /// Find blocks until <see cref="BlockStoreInnerStepContext.DownloadStack"/> contains 
    /// <see cref="BlockStoreInnerStepContext.DownloadStackThreshold"/> blocks.
    /// </para>
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocksAsync"/> and
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
                if (await ShouldStopFindingBlocksAsync(context))
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

        private async Task<bool> ShouldStopFindingBlocksAsync(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            if (context.NextChainedBlock == null)
            {
                this.logger.LogTrace("(-)[NULL_NEXT]:true");
                return true;
            }

            if ((context.InputChainedBlock != null) && (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-)[NEXT_NEQ_INPUT]:true");
                return true;
            }

            if (context.NextChainedBlock.Height > context.BlockStoreLoop.ChainState.ConsensusTip?.Height)
            {
                this.logger.LogTrace("(-)[NEXT_HEIGHT_GT_CONSENSUS_TIP]:true");
                return true;
            }

            if (context.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("Chained block '{0}' already exists in the pending storage.", context.NextChainedBlock);
                this.logger.LogTrace("(-)[NEXT_ALREADY_EXISTS_PENDING_STORE]:true");
                return true;
            }

            if (await context.BlockStoreLoop.BlockRepository.ExistAsync(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("Chained block '{0}' already exists in the repository.", context.NextChainedBlock);
                this.logger.LogTrace("(-)[NEXT_ALREADY_EXISTS_REPOSITORY]:true");
                return true;
            }

            this.logger.LogTrace("(-):false");
            return false;
        }
    }
}