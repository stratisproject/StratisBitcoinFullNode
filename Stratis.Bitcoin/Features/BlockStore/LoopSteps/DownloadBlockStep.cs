using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Continuously find and download blocks until a stop condition is found.
    ///<para>
    ///<list>
    /// There are two operations:
    ///     <item>1: <see cref="BlockStoreInnerStepFindBlocks"/> to ask the block puller to download the blocks.</item>
    ///     <item>2: <see cref="BlockStoreInnerStepDownloadBlocks"/> to persist the blocks in a batch to the <see cref="BlockRepository"/>.</item>
    /// </list>
    /// </para> 
    /// <para>
    /// After a "Stop" condition is found the FindBlocksTask will be removed from 
    /// the <see cref="BlockStoreInnerStepContext.InnerSteps"/> and only the 
    /// <see cref="BlockStoreInnerStepDownloadBlocks"/> will continue to execute until the <see cref="BlockStoreInnerStepContext.DownloadStack"/> is empty.
    /// </para>   
    /// </summary>
    internal sealed class DownloadBlockStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc/>
        internal override async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken token, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(nextChainedBlock), nextChainedBlock?.HashBlock, nextChainedBlock?.Height, nameof(disposeMode), disposeMode);

            if (disposeMode)
            {
                this.logger.LogTrace("(-):{0}", StepResult.Stop);
                return StepResult.Stop;
            }

            var context = new BlockStoreInnerStepContext(token, this.BlockStoreLoop, this.loggerFactory, this.dateTimeProvider).Initialize(nextChainedBlock);

            this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);

            while (!token.IsCancellationRequested)
            {
                foreach (var innerStep in context.InnerSteps.ToList())
                {
                    InnerStepResult innerStepResult = await innerStep.ExecuteAsync(context);
                    if (innerStepResult == InnerStepResult.Stop)
                    {
                        this.logger.LogTrace("(-):{0}", StepResult.Next);
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