using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The chain of block store loop steps that is executed when the
    /// BlockStoreLoop's DownloadAndStoreBlocks is called.
    /// <seealso cref="BlockStoreLoop.DownloadAndStoreBlocksAsync"/>
    /// </summary>
    internal sealed class BlockStoreStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();

        /// <summary>Set the next step to execute in the BlockStoreLoop.</summary>
        /// <param name="step">The next step to execute.</param>
        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        /// <summary>
        /// Executes the chain of <see cref="BlockStoreLoop"/> steps.
        /// <para>
        /// Each step will return a <see cref="StepResult"/> which will either:
        /// <list>
        ///     <item>1: Break out of the foreach loop.</item>
        ///     <item>2: Continue execution of the foreach loop.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nextChainedBlock">Next chained block to process.</param>
        /// <param name="disposeMode">This is <c>true</c> if <see cref="BlockStoreLoop.ShutDown"/> was called.</param>
        /// <param name="cancellationToken">Cancellation token to check.</param>
        /// <returns>BlockStoreLoopStepResult</returns>
        internal async Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, bool disposeMode, CancellationToken cancellationToken)
        {
            foreach (var step in this.steps)
            {
                var stepResult = await step.ExecuteAsync(nextChainedBlock, cancellationToken, disposeMode);
                if ((stepResult == StepResult.Continue) || (stepResult == StepResult.Stop))
                    return stepResult;
            }

            return StepResult.Next;
        }
    }

    /// <summary>Base class for each block store step.</summary>
    internal abstract class BlockStoreLoopStep
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));

            this.loggerFactory = loggerFactory;
            this.BlockStoreLoop = blockStoreLoop;
        }

        internal BlockStoreLoop BlockStoreLoop;

        internal abstract Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    /// <summary>
    /// The result that is returned from executing each loop step.
    /// </summary>   
    public enum StepResult
    {
        /// <summary>Continue execution of the loop.</summary>
        Continue,

        /// <summary>Execute the next line of code in the loop.</summary>
        Next,

        /// <summary>Break out of the loop.</summary>
        Stop,
    }
}