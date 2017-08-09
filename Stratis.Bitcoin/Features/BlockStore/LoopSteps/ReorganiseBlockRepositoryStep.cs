using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    internal sealed class ReorganiseBlockRepositoryStep : BlockStoreLoopStep
    {
        internal ReorganiseBlockRepositoryStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (this.BlockStoreLoop.StoreTip.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return BlockStoreLoopStepResult.Break();

                var blocksToDelete = new List<uint256>();
                var blockToDelete = this.BlockStoreLoop.StoreTip;

                //The chained block does not exist on the chain
                //Add blocks to delete to the blocksToDelete collection by walking back the chain until the last chained block is found
                while (this.BlockStoreLoop.Chain.GetBlock(blockToDelete.HashBlock) == null)
                {
                    blocksToDelete.Add(blockToDelete.HashBlock);
                    blockToDelete = blockToDelete.Previous;
                }

                //Delete blocks from the repository
                await this.BlockStoreLoop.BlockRepository.DeleteAsync(blockToDelete.HashBlock, blocksToDelete);

                //Set the last stored block to the last found chained block
                this.BlockStoreLoop.SetStoreTip(blockToDelete);

                return BlockStoreLoopStepResult.Break();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}