using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The chain of block store loop steps that is executed when the
    /// BlockStoreLoop's DownloadAndStoreBlocks is called.
    /// <seealso cref="BlockStoreLoop.DownloadAndStoreBlocks"/>
    /// </summary>
    internal sealed class BlockStoreStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();

        /// <summary>Set the next step to execute in the BlockStoreLoop.</summary>
        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        /// <summary>
        /// Executes the chain of BlockStoreLoop steps.
        /// <para>
        /// Each step will return a BlockStoreLoopStepResult which will either:
        /// <list>
        ///     <item>1: Break out of the ForEach</item>
        ///     <item>2: Continue execution of the ForEach</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nextChainedBlock">Next chained block to process</param>
        /// <param name="disposeMode">This will <c>true</c> if Flush() was called on the BlockStore</param>
        /// <param name="cancellationToken">Cancellation token to check</param>
        /// <returns>BlockStoreLoopStepResult</returns>
        internal async Task<StepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode, CancellationToken cancellationToken)
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
        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));

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