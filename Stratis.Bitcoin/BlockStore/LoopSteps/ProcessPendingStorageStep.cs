using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore.LoopSteps
{
    /// <summary>
    /// Check if the next block is in pending storage
    /// If so loop over the pending blocks and push to the repository in batches
    /// if a stop condition is met break from the loop back to the start
    /// </summary>
    internal sealed class ProcessPendingStorageStep : BlockStoreLoopStep
    {
        internal ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop, CancellationToken cancellationToken)
            : base(blockStoreLoop, cancellationToken)
        {
        }

        private List<BlockPair> pendingBlockPairsToStore = new List<BlockPair>();
        private int pendingStorageBatchSize = 0;
        private BlockPair pendingBlockPairToStore;

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            // If in IBD and batch count is not yet reached then wait
            if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload && !disposeMode)
            {
                // SKip(0) returns an enumerator which doesn't re-count the collection
                if (this.BlockStoreLoop.PendingStorage.Skip(0).Count() < this.BlockStoreLoop.PendingStorageBatchThreshold)
                    return BlockStoreLoopStepResult.Break();
            }

            // Remove the BlockPair from PendingStorage and return for further processing
            // If the next chained block does not exist, continue with execution
            if (!this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
                return BlockStoreLoopStepResult.Next();

            this.pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);
            this.pendingStorageBatchSize = this.pendingBlockPairToStore.Block.GetSerializedSize();

            while (!this.CancellationToken.IsCancellationRequested)
            {
                var previousChainedBlock = nextChainedBlock;
                nextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

                var breakExecution = ShouldBreakExecution(previousChainedBlock, nextChainedBlock);

                if (!breakExecution && !this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
                    breakExecution = true;

                if (breakExecution && this.pendingBlockPairsToStore.Count() == 0)
                    break;

                this.pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);

                var result = await PushPendingBlocksToRepository(previousChainedBlock, breakExecution);
                if (result.ShouldBreak)
                    break;
            }

            return BlockStoreLoopStepResult.Continue();
        }

        private async Task<BlockStoreLoopStepResult> PushPendingBlocksToRepository(ChainedBlock previousChainedBlock, bool breakExecution)
        {
            // TODO: add the size to the result coming from the signaler	
            this.pendingStorageBatchSize += this.pendingBlockPairToStore.Block.GetSerializedSize();

            if (this.pendingStorageBatchSize > this.BlockStoreLoop.InsertBlockSizeThreshold || breakExecution)
            {
                // Store missing blocks and remove them from pending blocks
                await this.BlockStoreLoop.BlockRepository.PutAsync(previousChainedBlock.HashBlock, this.pendingBlockPairsToStore.Select(b => b.Block).ToList());

                this.BlockStoreLoop.StoredBlock = previousChainedBlock;
                this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

                if (breakExecution)
                    return BlockStoreLoopStepResult.Break();

                this.pendingBlockPairsToStore.Clear();
                this.pendingStorageBatchSize = 0;

                // this can be tweaked if insert is effecting the consensus speed
                if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload)
                    await Task.Delay(this.BlockStoreLoop.pushIntervalIBD, this.CancellationToken);
            }

            return BlockStoreLoopStepResult.Next();
        }

        /// <summary>
        /// Break execution if at the tip or block is already in store or pending insertion
        /// </summary>
        private bool ShouldBreakExecution(ChainedBlock previousChainedBlock, ChainedBlock nextChainedBlock)
        {
            if (nextChainedBlock == null)
                return true;

            if (nextChainedBlock.Header.HashPrevBlock != previousChainedBlock.HashBlock)
                return true;

            if (nextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return true;

            return false;
        }
    }
}