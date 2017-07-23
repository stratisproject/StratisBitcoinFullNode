using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore.LoopSteps
{
    internal sealed class CheckNextChainedBlockExistStep : BlockStoreLoopStep
    {
        internal CheckNextChainedBlockExistStep(BlockStoreLoop blockStoreLoop, CancellationToken cancellationToken)
            : base(blockStoreLoop, cancellationToken)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
            {
                // Next block is in storage update StoredBlock 
                await this.BlockStoreLoop.BlockRepository.SetBlockHash(nextChainedBlock.HashBlock);
                this.BlockStoreLoop.StoredBlock = nextChainedBlock;
                this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

                return BlockStoreLoopStepResult.Continue();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}
