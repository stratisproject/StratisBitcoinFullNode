using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// NextChainedBlock block exists, update StoredBlock 
    /// </summary>
    internal sealed class CheckNextChainedBlockExistStep : BlockStoreLoopStep
    {
        internal CheckNextChainedBlockExistStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
            {
                await this.BlockStoreLoop.BlockRepository.SetBlockHash(nextChainedBlock.HashBlock);

                this.BlockStoreLoop.StoredBlock = nextChainedBlock;
                this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

                return new BlockStoreLoopStepResult().Continue();
            }

            return new BlockStoreLoopStepResult().Next();
        }
    }
}