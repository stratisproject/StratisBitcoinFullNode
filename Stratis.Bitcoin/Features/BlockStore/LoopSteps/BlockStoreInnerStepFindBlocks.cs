using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Find blocks to download by asking the BlockPuller.
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there aren't blocks to download anymore, return a Break() result causing the 
    /// BlockStoreLoop to break execution and start again.
    /// </para>
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there are still blocks to download, stop finding new blocks and only execute
    /// the download blocks inner step.
    /// </para> 
    /// <para>
    /// If a stop condition is not found ask the block puller for the next blocks.
    /// If the BatchDownloadSize has been reached, also stop finding new blocks.
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
            context.GetNextBlock();

            if (await ShouldStopFindingBlocks(context))
            {
                if (!context.DownloadStack.Any())
                {
                    this.logger.LogTrace("(-):{0}", InnerStepResult.Stop);
                    return InnerStepResult.Stop;
                }

                context.StopFindingBlocks();
            }
            else
            {
                context.BlockStoreLoop.BlockPuller.AskBlock(context.NextChainedBlock);
                context.DownloadStack.Enqueue(context.NextChainedBlock);

                if (context.DownloadStack.Count == context.BlockStoreLoop.BatchDownloadSize)
                    context.StopFindingBlocks();
            }

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);
            return InnerStepResult.Next;
        }

        private async Task<bool> ShouldStopFindingBlocks(BlockStoreInnerStepContext context)
        {
            this.logger.LogTrace("()");

            bool res = (context.NextChainedBlock == null)
                || (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock)
                || (context.NextChainedBlock.Height > context.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                || (context.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
                || (await context.BlockStoreLoop.BlockRepository.ExistAsync(context.NextChainedBlock.HashBlock));

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
    }
}