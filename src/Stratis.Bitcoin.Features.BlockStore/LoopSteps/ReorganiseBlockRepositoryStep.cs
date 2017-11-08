using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Reorganises the <see cref="BlockRepository"/>.
    /// <para>
    /// This will happen when the block store's tip does not match
    /// the next chained block's previous header.
    /// </para>
    /// <para>
    /// Steps:
    /// <list type="bullet">
    ///     <item>1: Add blocks to delete from the repository by walking back the chain until the last chained block is found.</item>
    ///     <item>2: Delete those blocks from the BlockRepository.</item>
    ///     <item>3: Set the last stored block (tip) to the last found chained block.</item>
    /// </list>
    /// </para>
    /// <para>
    /// If the store/repository does not require reorganising the step will 
    /// return Next which will cause the <see cref="BlockStoreLoop" /> to 
    /// execute the next step <see cref="CheckNextChainedBlockExistStep"/>. 
    /// If not the step will cause the <see cref="BlockStoreLoop" /> to break execution and start again.
    /// </para>
    /// </summary>
    internal sealed class ReorganiseBlockRepositoryStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        internal ReorganiseBlockRepositoryStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            if (this.BlockStoreLoop.StoreTip.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                {
                    this.logger.LogTrace("(-)[DISPOSE]:{0}", StepResult.Stop);
                    return StepResult.Stop;
                }

                var blocksToDelete = new List<uint256>();
                ChainedBlock blockToDelete = this.BlockStoreLoop.StoreTip;

                while (this.BlockStoreLoop.Chain.GetBlock(blockToDelete.HashBlock) == null)
                {
                    blocksToDelete.Add(blockToDelete.HashBlock);
                    blockToDelete = blockToDelete.Previous;
                }

                await this.BlockStoreLoop.BlockRepository.DeleteAsync(blockToDelete.HashBlock, blocksToDelete);

                this.BlockStoreLoop.SetStoreTip(blockToDelete);

                this.logger.LogTrace("(-)[MISMATCH]:{0}", StepResult.Stop);
                return StepResult.Stop;
            }

            this.logger.LogTrace("(-):{0}", StepResult.Next);
            return StepResult.Next;
        }
    }
}