using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    internal sealed class BlockStoreLoopStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();
        private bool disposeMode;

        public BlockStoreLoopStepChain(bool disposeMode)
        {
            this.disposeMode = disposeMode;
        }

        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        internal async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken)
        {
            var result = new BlockStoreLoopStepResult().Next();

            foreach (var step in this.steps)
            {
                var stepResult = await step.Execute(nextChainedBlock, cancellationToken, this.disposeMode);

                if (stepResult.ShouldBreak)
                {
                    result = stepResult;
                    break;
                }

                if (stepResult.ShouldContinue)
                {
                    result = stepResult;
                    break;
                }
            }

            return result;
        }
    }

    internal abstract class BlockStoreLoopStep
    {
        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop)
        {
            Guard.NotNull(blockStoreLoop, "blockStoreLoop");

            this.BlockStoreLoop = blockStoreLoop;
        }

        internal BlockStoreLoop BlockStoreLoop;

        internal abstract Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    internal class BlockStoreLoopStepResult
    {
        internal BlockStoreLoopStepResult() { }

        internal bool ShouldBreak { get; private set; }
        internal bool ShouldContinue { get; private set; }

        internal BlockStoreLoopStepResult Break()
        {
            this.ShouldBreak = true;
            return this;
        }

        internal BlockStoreLoopStepResult Continue()
        {
            this.ShouldContinue = true;
            return this;
        }

        internal BlockStoreLoopStepResult Next()
        {
            return this;
        }
    }
}