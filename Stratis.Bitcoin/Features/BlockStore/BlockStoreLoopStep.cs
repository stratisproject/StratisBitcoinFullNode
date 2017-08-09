using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The chain of block store loop steps that is executed when the
    /// BlockStoreLoop's DownloadAndStoreBlocks is called
    /// <seealso cref="BlockStoreLoop.DownloadAndStoreBlocks"/>
    /// </summary>
    internal sealed class BlockStoreStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();

        /// <summary>
        /// Sets the next to execute in the chain
        /// </summary>
        /// <param name="step"></param>
        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        /// <summary>
        /// Executes the chain of BlockStoreLoop steps
        /// <para>
        /// Each step will return a BlockStoreLoopStepResult which will either:
        ///     1: Break out of the ForEach
        ///     2: Continue execution of the ForEach
        /// </para>
        /// </summary>
        /// <param name="nextChainedBlock"></param>
        /// <param name="disposeMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>BlockStoreLoopStepResult</returns>
        internal async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode, CancellationToken cancellationToken)
        {
            var result = BlockStoreLoopStepResult.Next();

            foreach (var step in this.steps)
            {
                var stepResult = await step.ExecuteAsync(nextChainedBlock, cancellationToken, disposeMode);
                if (stepResult.ShouldBreak || stepResult.ShouldContinue)
                {
                    result = stepResult;
                    break;
                }
            }

            return result;
        }
    }

    /// <summary> 
    /// Base class for each BlockStoreLoopStep
    /// <para>
    /// Each step must implement ExecuteAsync passing in the next chained block to process
    /// </para>
    /// </summary>
    internal abstract class BlockStoreLoopStep
    {
        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop)
        {
            Guard.NotNull(blockStoreLoop, "blockStoreLoop");

            this.BlockStoreLoop = blockStoreLoop;
        }

        /// <inheritdoc />
        internal BlockStoreLoop BlockStoreLoop;

        /// <inheritdoc />
        internal abstract Task<BlockStoreLoopStepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    /// <summary>
    /// The result class that gets return from executing each loop step
    /// <para>
    /// The caller, based on the result, will either:
    ///     1: "Break" > Break out of a loop
    ///     2: "Continue" > Continue execution of the loop
    ///     3: "Next" > Execute the next line of code
    /// </para>
    /// </summary>
    public sealed class BlockStoreLoopStepResult
    {
        internal BlockStoreLoopStepResult() { }

        public bool ShouldBreak { get; private set; }
        public bool ShouldContinue { get; private set; }

        /// <inheritdoc />
        internal static BlockStoreLoopStepResult Break()
        {
            var result = new BlockStoreLoopStepResult();
            result.ShouldBreak = true;
            return result;
        }

        /// <inheritdoc />
        internal static BlockStoreLoopStepResult Continue()
        {
            var result = new BlockStoreLoopStepResult();
            result.ShouldContinue = true;
            return result;
        }

        /// <inheritdoc />
        internal static BlockStoreLoopStepResult Next()
        {
            var result = new BlockStoreLoopStepResult();
            return result;
        }
    }
}