using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
    internal sealed class BlockStoreLoopStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();
        private ChainedBlock nextChainedBlock;
        private bool disposeMode;

        public BlockStoreLoopStepChain(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            this.nextChainedBlock = nextChainedBlock;
            this.disposeMode = disposeMode;
        }

        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        internal async Task<BlockStoreLoopStepResult> Execute()
        {
            BlockStoreLoopStepResult result = null;

            foreach (var step in this.steps)
            {
                var stepResult = await step.Execute(this.nextChainedBlock, this.disposeMode);

                if (stepResult.ShouldBreak)
                {
                    result = stepResult;
                    break;
                }

                if (result.ShouldContinue)
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
        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop, CancellationToken cancellationToken)
        {
            Guard.NotNull(blockStoreLoop, "blockStoreLoop");
            Guard.NotNull(cancellationToken, "cancellationToken");

            this.BlockStoreLoop = blockStoreLoop;
            this.CancellationToken = cancellationToken;
        }

        internal BlockStoreLoop BlockStoreLoop;
        internal CancellationToken CancellationToken;

        internal abstract Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode);
    }

    internal sealed class BlockStoreLoopStepResult
    {
        private BlockStoreLoopStepResult() { }

        internal bool ShouldBreak { get; private set; }
        internal bool ShouldContinue { get; private set; }

        internal static BlockStoreLoopStepResult Break()
        {
            return new BlockStoreLoopStepResult() { ShouldBreak = true };
        }

        internal static BlockStoreLoopStepResult Continue()
        {
            return new BlockStoreLoopStepResult() { ShouldContinue = true };
        }

        internal static BlockStoreLoopStepResult Next()
        {
            return new BlockStoreLoopStepResult();
        }
    }
}