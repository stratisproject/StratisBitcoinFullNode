using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    public sealed class BlockStoreStepTaskContext
    {
        public BlockStoreStepTaskContext(CancellationToken cancellationToken, BlockStoreLoop blockStoreLoop)
        {
            Guard.NotNull(blockStoreLoop, "blockStoreLoop");

            this.BlockStoreLoop = blockStoreLoop;
            this.CancellationToken = cancellationToken;
        }

        public BlockStoreStepTaskContext Initialize(ChainedBlock nextChainedBlock)
        {
            Guard.NotNull(nextChainedBlock, "nextChainedBlock");

            this.DownloadStack = new Queue<ChainedBlock>(new[] { nextChainedBlock });
            this.NextChainedBlock = nextChainedBlock;
            this.Routine = new List<BlockStoreStepTask>() { new BlockStoreStepFindBlocksTask(), new BlockStoreStepDownloadBlocksTask() };

            this.InsertBlockSize = 0;
            this.StallCount = 0;
            this.Store = new List<BlockPair>();

            this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);

            return this;
        }

        public Queue<ChainedBlock> DownloadStack { get; private set; }
        public BlockStoreLoop BlockStoreLoop { get; private set; }
        public ChainedBlock InputChainedBlock { get; private set; }
        public ChainedBlock NextChainedBlock { get; private set; }
        public List<BlockStoreStepTask> Routine { get; private set; }

        public CancellationToken CancellationToken;
        public List<BlockPair> Store;
        public int InsertBlockSize;
        public int StallCount;

        internal void GetNextBlock()
        {
            this.InputChainedBlock = this.NextChainedBlock;
            this.NextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(this.InputChainedBlock.Height + 1);
        }

        internal void StopFindingBlocks()
        {
            this.Routine.Remove(this.Routine.OfType<BlockStoreStepFindBlocksTask>().First());
        }
    }

    public abstract class BlockStoreStepTask
    {
        public abstract Task<BlockStoreLoopStepResult> ExecuteAsync(BlockStoreStepTaskContext context);
    }
}
