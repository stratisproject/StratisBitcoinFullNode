using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Reorganises the BlockRepository.
    /// <para>
    /// This will happen when the BlockStore's tip does not match
    /// the next chained block's previous header.
    /// </para>
    /// <para>
    /// Steps:
    ///     1: Add blocks to delete from the repository by walking back the chain until the last chained block is found.
    ///     2: Delete those blocks from the BlockRepository.
    ///     3: Set the last stored block (tip) to the last found chained block.
    /// </para>
    /// <para>
    /// If the store/repository does not require reorganising the step will 
    /// return Next() which will cause the BlockStoreLoop to 
    /// execute the next step <see cref="CheckNextChainedBlockExistStep"/>. 
    /// If not the step will cause the BlockStoreLoop to break execution and start again.
    /// </para>
    /// </summary>
    internal sealed class ReorganiseBlockRepositoryStep : BlockStoreLoopStep
    {
        internal ReorganiseBlockRepositoryStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (this.BlockStoreLoop.StoreTip.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return StepResult.Stop;

                var blocksToDelete = new List<uint256>();
                var blockToDelete = this.BlockStoreLoop.StoreTip;

                while (this.BlockStoreLoop.Chain.GetBlock(blockToDelete.HashBlock) == null)
                {
                    blocksToDelete.Add(blockToDelete.HashBlock);
                    blockToDelete = blockToDelete.Previous;
                }

                await this.BlockStoreLoop.BlockRepository.DeleteAsync(blockToDelete.HashBlock, blocksToDelete);

                this.BlockStoreLoop.SetStoreTip(blockToDelete);

                return StepResult.Stop;
            }

            return StepResult.Next;
        }
    }
}