namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    using Microsoft.Extensions.Logging;
    using NBitcoin;
    using System.Threading;
    using System.Threading.Tasks;

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
            this.logger.LogTrace("{0}:'{1}/{2}',{3}:{4}", nameof(nextChainedBlock), nextChainedBlock?.HashBlock, nextChainedBlock?.Height, nameof(disposeMode), disposeMode);

            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
            {
                await this.BlockStoreLoop.BlockRepository.SetBlockHash(nextChainedBlock.HashBlock);

                this.BlockStoreLoop.SetStoreTip(nextChainedBlock);

                return StepResult.Continue;
            }

            return StepResult.Next;
        }
    }
}