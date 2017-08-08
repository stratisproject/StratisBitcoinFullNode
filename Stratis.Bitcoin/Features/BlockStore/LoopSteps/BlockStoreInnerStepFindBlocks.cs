using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Find blocks to download by asking the BlockPuller
    /// 
    /// Once the BatchDownloadSize has been reached
    /// the task will be removed from the routine via context.StopFindingBlocks()
    /// </summary>
    public sealed class BlockStoreInnerStepFindBlocks : BlockStoreStepTask
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