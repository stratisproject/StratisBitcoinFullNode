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

    /// <summary>Base class for each block store step.</summary>
    internal abstract class BlockStoreLoopStep
    {
        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));

            this.BlockStoreLoop = blockStoreLoop;
        }

        internal BlockStoreLoop BlockStoreLoop;

        internal abstract Task<BlockStoreLoopStepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    /// <summary>
    /// The result class that is returned from executing each loop step.
    /// <para>
    /// The caller, based on the result, will either:
    /// <list>
    ///     <item>1: "Break" > Break out of a loop.</item>
    ///     <item>2: "Continue" > Continue execution of the loop.</item>
    ///     <item>3: "Next" > Execute the next line of code.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class BlockStoreLoopStepResult
    {
        private BlockStoreLoopStepResult() { }

        public bool ShouldBreak { get; private set; }
        public bool ShouldContinue { get; private set; }

        /// <summary>
        /// Will cause the caller to break out of the iteration.
        /// </summary>
        internal static BlockStoreLoopStepResult Break()
        {
            var result = new BlockStoreLoopStepResult();
            result.ShouldBreak = true;
            return result;
        }

        /// <summary>
        /// Will cause the caller to Continue execution of the loop i.e. go onto the next item in the iteration.
        /// </summary>
        internal static BlockStoreLoopStepResult Continue()
        {
            var result = new BlockStoreLoopStepResult();
            result.ShouldContinue = true;
            return result;
        }

        /// <summary>
        /// Will cause the caller to execute the next line of code.
        /// </summary>
        internal static BlockStoreLoopStepResult Next()
        {
            var result = new BlockStoreLoopStepResult();
            return result;
        }
    }
}