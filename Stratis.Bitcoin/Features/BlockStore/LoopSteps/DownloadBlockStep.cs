using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Continuously find and download blocks until a stop condition is found.
    ///<para>
    /// There are two operations:
    ///     1: FindBlocks() to download by asking them from the BlockPuller
    ///     2: DownloadBlocks() and persisting them as a batch to the BlockRepository
    ///      
    /// After a "Stop" condition is found the FindBlocksTask will be removed from 
    /// the routine<see cref="BlockStoreInnerStepContext.Routine"/> and only the 
    /// DownloadBlocksTask will continue to execute until the DownloadStack is empty
    /// </para>   
    /// </summary>
    internal sealed class DownloadBlockStep : BlockStoreLoopStep
    {
        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        /// <inheritdoc/>
        internal override async Task<BlockStoreLoopStepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken token, bool disposeMode)
        {
            if (disposeMode)
                return BlockStoreLoopStepResult.Break();

            var result = BlockStoreLoopStepResult.Next();

            var context = new BlockStoreInnerStepContext(token, this.BlockStoreLoop).Initialize(nextChainedBlock);

            this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);

            while (!token.IsCancellationRequested)
            {
                foreach (var item in context.Routine.ToList())
                {
                    var executionResult = await item.ExecuteAsync(context);
                    if (executionResult.ShouldBreak)
                        return result;
                }
            }

            return result;
        }
    }
}