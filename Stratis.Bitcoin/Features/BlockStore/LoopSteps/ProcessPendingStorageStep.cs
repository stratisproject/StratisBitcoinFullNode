using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Check if the next block is in pending storage
    /// If so loop over the pending blocks and push to the repository in batches
    /// if a stop condition is met break from the loop back to the start
    /// </summary>
    internal sealed class ProcessPendingStorageStep : BlockStoreLoopStep
    {
        private BlockPair pendingBlockPairToStore;

        internal ProcessPendingStorageStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            // Remove the BlockPair from PendingStorage and return for further processing
            // If the next chained block does not exist, continue with execution
            if (!this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
                return BlockStoreLoopStepResult.Next();

            // If in IBD and batch count is not yet reached then wait
            if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload && !disposeMode)
            {
                if (this.BlockStoreLoop.PendingStorage.Skip(0).Count() < this.BlockStoreLoop.PendingStorageBatchThreshold) // Skip(0) returns an enumerator which doesn't re-count the collection
                    return BlockStoreLoopStepResult.Break();
            }

            var pendingBlockPairsToStore = new List<BlockPair>();
            pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);
            var pendingStorageBatchSize = this.pendingBlockPairToStore.Block.GetSerializedSize();

            var lastFoundChainedBlock = nextChainedBlock;

            while (!cancellationToken.IsCancellationRequested)
            {
                var inputChainedBlock = nextChainedBlock;
                nextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

                var breakExecution = ShouldBreakExecution(inputChainedBlock, nextChainedBlock);

                if (!breakExecution && !this.BlockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out this.pendingBlockPairToStore))
                    breakExecution = true;

                if (breakExecution)
                {
                    if (!pendingBlockPairsToStore.Any())
                        break;
                }
                else
                {
                    pendingBlockPairsToStore.Add(this.pendingBlockPairToStore);
                    pendingStorageBatchSize += this.pendingBlockPairToStore.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler	
                    lastFoundChainedBlock = nextChainedBlock;
                }

                if (pendingStorageBatchSize > this.BlockStoreLoop.InsertBlockSizeThreshold || breakExecution)
                {
                    var result = await PushPendingBlocksToRepository(pendingStorageBatchSize, pendingBlockPairsToStore, lastFoundChainedBlock, cancellationToken, breakExecution);
                    if (result.ShouldBreak)
                        break;

                    pendingBlockPairsToStore.Clear();
                    pendingStorageBatchSize = 0;

                    if (this.BlockStoreLoop.ChainState.IsInitialBlockDownload) // this can be tweaked if insert is effecting the consensus speed
                        await Task.Delay(this.BlockStoreLoop.PushIntervalIBD, cancellationToken);
                }
            }

            pendingBlockPairsToStore.Clear();
            pendingBlockPairsToStore = null;

            this.pendingBlockPairToStore = null;

            return BlockStoreLoopStepResult.Continue();
        }

        /// <summary>
        /// Store missing blocks and remove them from pending blocks
        /// </summary>
        private async Task<BlockStoreLoopStepResult> PushPendingBlocksToRepository(int pendingStorageBatchSize, List<BlockPair> pendingBlockPairsToStore, ChainedBlock lastFoundChainedBlock, CancellationToken cancellationToken, bool breakExecution)
        {
            await this.BlockStoreLoop.BlockRepository.PutAsync(lastFoundChainedBlock.HashBlock, pendingBlockPairsToStore.Select(b => b.Block).ToList());

            this.BlockStoreLoop.StoredBlock = lastFoundChainedBlock;
            this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;

            if (breakExecution)
                return BlockStoreLoopStepResult.Break();

            return BlockStoreLoopStepResult.Next();
        }

        /// <summary>
        /// Break execution if at the tip or block is already in store or pending insertion
        /// </summary>
        private bool ShouldBreakExecution(ChainedBlock inputChainedBlock, ChainedBlock nextChainedBlock)
        {
            if (nextChainedBlock == null)
                return true;

            if (nextChainedBlock.Header.HashPrevBlock != inputChainedBlock.HashBlock)
                return true;

            if (nextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return true;

            return false;
        }
    }
}