using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore.LoopSteps
{
    internal sealed class ReorganiseBlockRepositoryStep : BlockStoreLoopStep
    {
        internal ReorganiseBlockRepositoryStep(BlockStoreLoop blockStoreLoop, CancellationToken cancellationToken)
            : base(blockStoreLoop, cancellationToken)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            if (this.BlockStoreLoop.StoredBlock.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return new BlockStoreLoopStepResult().Break();

                var blocksToDelete = new List<uint256>();
                var delete = this.BlockStoreLoop.StoredBlock;

                //The chained block does not exist on the chain
                //Add blocks to delete to the blocksToDelete collection by walking back the chain until the last chained block is found
                while (this.BlockStoreLoop.Chain.GetBlock(delete.HashBlock) == null)
                {
                    blocksToDelete.Add(delete.HashBlock);
                    delete = delete.Previous;
                }

                //Delete the un-persisted blocks from the repository
                await this.BlockStoreLoop.BlockRepository.DeleteAsync(delete.HashBlock, blocksToDelete);

                //Set the last stored block to the last found chained block
                this.BlockStoreLoop.StoredBlock = delete;
                this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

                return new BlockStoreLoopStepResult().Break();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}