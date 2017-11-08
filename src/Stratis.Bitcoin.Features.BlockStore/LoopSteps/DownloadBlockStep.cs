using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Continuously find and download blocks until a stop condition is found.
    ///<para>
    ///<list>
    /// There are two operations:
    ///     <item>1: <see cref="BlockStoreInnerStepFindBlocks"/> to ask the block puller to download the blocks.</item>
    ///     <item>2: <see cref="BlockStoreInnerStepReadBlocks"/> to persist the blocks in a batch to the <see cref="BlockRepository"/>.</item>
    /// </list>
    /// </para> 
    /// <para>
    /// After a "Stop" condition is found the <see cref="BlockStoreInnerStepFindBlocks"/> will be removed from
    /// <see cref="BlockStoreInnerStepContext.InnerSteps"/> and only the 
    /// <see cref="BlockStoreInnerStepReadBlocks"/> task will continue to execute 
    /// until the <see cref="BlockStoreInnerStepContext.DownloadStack"/> is empty.
    /// </para>   
    /// </summary>
    internal sealed class DownloadBlockStep : BlockStoreLoopStep
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken token, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            if (disposeMode)
            {
                this.logger.LogTrace("(-)[DISPOSE]:{0}", StepResult.Stop);
                return StepResult.Stop;
            }

            var context = new BlockStoreInnerStepContext(token, this.BlockStoreLoop, nextChainedBlock, this.loggerFactory, this.dateTimeProvider);
            while (!token.IsCancellationRequested)
            {
                foreach (BlockStoreInnerStep innerStep in context.InnerSteps.ToList())
                {
                    InnerStepResult innerStepResult = await innerStep.ExecuteAsync(context);
                    if (innerStepResult == InnerStepResult.Stop)
                    {
                        this.logger.LogTrace("(-)[INNER]:{0}", StepResult.Next);
                        return StepResult.Next;
                    }
                }
            }

            this.logger.LogTrace("(-):{0}", StepResult.Next);
            return StepResult.Next;
        }
    }

    /// <summary>
    /// The result that is returned from executing each inner step.
    /// </summary>   
    public enum InnerStepResult
    {
        /// <summary>Execute the next line of code in the loop.</summary>
        Next,

        /// <summary>Break out of the loop.</summary>
        Stop
    }
}