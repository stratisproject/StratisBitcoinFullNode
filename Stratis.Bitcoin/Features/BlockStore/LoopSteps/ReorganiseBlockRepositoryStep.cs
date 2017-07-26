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

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (this.BlockStoreLoop.StoredBlock.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return new BlockStoreLoopStepResult().Break();

                var blocksToDelete = new List<uint256>();
                var blockToDelete = this.BlockStoreLoop.StoredBlock;

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
                this.BlockStoreLoop.StoredBlock = blockToDelete;
                this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

                blocksToDelete.Clear();
                blocksToDelete = null;
                blockToDelete = null;

                return new BlockStoreLoopStepResult().Break();
            }

            return new BlockStoreLoopStepResult().Next();
        }
    }
}