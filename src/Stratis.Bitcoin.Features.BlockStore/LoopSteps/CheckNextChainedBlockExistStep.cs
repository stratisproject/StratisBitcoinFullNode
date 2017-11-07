using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Check if the next chained block already exists in the <see cref="BlockRepository"/>.
    /// <para>
    /// If the block exists in the repository the step will return a Continue result which executes 
    /// "Continue" on the while loop.
    /// </para>
    /// <para>
    /// If the block does not exists in the repository the step 
    /// will return a Next result which'll cause the <see cref="BlockStoreLoop"/> to execute 
    /// the next step (<see cref="ProcessPendingStorageStep"/>).
    /// </para>
    /// </summary>
    internal sealed class CheckNextChainedBlockExistStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        internal CheckNextChainedBlockExistStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
            {
                await this.BlockStoreLoop.BlockRepository.SetBlockHashAsync(nextChainedBlock.HashBlock);

                this.BlockStoreLoop.SetStoreTip(nextChainedBlock);

                this.logger.LogTrace("(-)[EXIST]:{0}", StepResult.Continue);
                return StepResult.Continue;
            }

            this.logger.LogTrace("(-):{0}", StepResult.Next);
            return StepResult.Next;
        }
    }
}