using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Find blocks to download by asking the BlockPuller
    /// <para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there arent blocks to download anymore return a Break() result causing the 
    /// BlockStoreLoop to break execution and start again
    /// </para>
    /// If a stop condition is found <see cref="ShouldStopFindingBlocks"/> and
    /// there are still blocks to download, stop finding new blocks and only execute
    /// the download blocks inner step
    /// </para> 
    /// <para>
    /// If a stop condition is not found ask the block puller for the next blocks.
    /// If the BatchDownloadSize has been reached, also stop finding new blocks.
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepFindBlocks : BlockStoreInnerStep
    {
        public override async Task<BlockStoreLoopStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            context.GetNextBlock();

            if (await ShouldStopFindingBlocks(context))
            {
                if (!context.DownloadStack.Any())
                    return BlockStoreLoopStepResult.Break();

                context.StopFindingBlocks();
            }
            else
            {
                context.BlockStoreLoop.BlockPuller.AskBlock(context.NextChainedBlock);
                context.DownloadStack.Enqueue(context.NextChainedBlock);

                if (context.DownloadStack.Count == context.BlockStoreLoop.BatchDownloadSize)
                    context.StopFindingBlocks();
            }

            return BlockStoreLoopStepResult.Next();
        }

        /// <inheritdoc/>
        private async Task<bool> ShouldStopFindingBlocks(BlockStoreInnerStepContext context)
        {
            if (context.NextChainedBlock == null)
                return true;

            if (context.NextChainedBlock.Header.HashPrevBlock != context.InputChainedBlock.HashBlock)
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