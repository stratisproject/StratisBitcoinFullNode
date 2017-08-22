using Microsoft.Extensions.Logging;
using NBitcoin;
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
    ///     <item>1: FindBlocks() to download by asking them from the BlockPuller.</item>
    ///     <item>2: DownloadBlocks() and persisting them as a batch to the BlockRepository.</item>
    /// </list>
    /// </para> 
    /// <para>
    /// After a "Stop" condition is found the FindBlocksTask will be removed from 
    /// the <see cref="BlockStoreInnerStepContext.InnerSteps"/> and only the 
    /// <see cref="BlockStoreInnerStepReadBlocks"/> will continue to execute until the DownloadStack is empty.
    /// </para>   
    /// </summary>
    internal sealed class DownloadBlockStep : BlockStoreLoopStep
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
            : base(blockStoreLoop, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
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

            var context = new BlockStoreInnerStepContext(token, this.BlockStoreLoop, nextChainedBlock, this.loggerFactory);

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