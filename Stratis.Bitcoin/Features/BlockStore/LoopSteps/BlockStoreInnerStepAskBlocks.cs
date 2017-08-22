using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Ask blocks to download by asking the BlockPuller.
    /// <para>
    /// Ask for blocks until <see cref="BlockStoreInnerStepContext.DownloadStack"/> copntains 10 blocks.
    /// </para>
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there are still blocks to download, stop finding new blocks and only execute
    /// the read blocks inner step <see cref="BlockStoreInnerStepReadBlocks"/>.
    /// </para> 
    /// </summary>
    public sealed class BlockStoreInnerStepAskBlocks : BlockStoreInnerStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public BlockStoreInnerStepAskBlocks(ILoggerFactory loggerFactory)
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
            this.logger.LogTrace("()");

            if (context.NextChainedBlock == null)
            {
                this.logger.LogTrace("(-): {0} was null, return true", nameof(context.NextChainedBlock));
                return true;
            }

            if (context.InputChainedBlock != null && (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-): {0} previous hash does not match {1} hash, return true.", nameof(context.NextChainedBlock), context.InputChainedBlock);
                return true;
            }

            if (context.NextChainedBlock.Height > context.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
            {
                this.logger.LogTrace("(-): {0} height was higher than {1}, return true.", nameof(context.NextChainedBlock), nameof(context.BlockStoreLoop.ChainState));
                return true;
            }

            if (context.BlockStoreLoop.PendingStorage.ContainsKey(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-): {0} containts {1}, return true", nameof(context.BlockStoreLoop.PendingStorage), nameof(context.NextChainedBlock));
                return true;
            }

            if (await context.BlockStoreLoop.BlockRepository.ExistAsync(context.NextChainedBlock.HashBlock))
            {
                this.logger.LogTrace("(-): {0} contains {1}, return true", nameof(context.BlockStoreLoop.BlockRepository), nameof(context.NextChainedBlock));
                return true;
            }

            if (context.DownloadStack.Count >= 10)
            {
                this.logger.LogTrace("(-): {0} threshold reached (10), return true", nameof(context.DownloadStack));
                return true;
            }

            this.logger.LogTrace("(-):{0}", InnerStepResult.Next);

            return false;
        }
    }
}