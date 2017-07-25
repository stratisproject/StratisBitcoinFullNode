using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore.LoopSteps
{
    // Continuously download blocks until a stop condition is found.
    // There are two operations:
    //      Find blocks to download by asking them from the BlockPuller
    //      Collecting downloaded blocks and persisting them as a batch to the BlockRepository

    internal sealed class DownloadBlockStep : BlockStoreLoopStep
    {
        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop)
            : base(blockStoreLoop)
        {
        }

        private int insertBlockSize = 0;
        private int stallCount = 0;
        private List<BlockPair> blockPairsToStore = new List<BlockPair>();

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (disposeMode)
            {
                Cleanup();
                return new BlockStoreLoopStepResult().Break();
            }

            var chainedBlockStackToDownload = new Queue<ChainedBlock>(new[] { nextChainedBlock });

            this.BlockStoreLoop.StoreBlockPuller.AskBlock(nextChainedBlock);

            var canDownload = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (canDownload)
                {
                    var askAndEnqueueResult = await AskAndEnqueueBlocksToDownload(chainedBlockStackToDownload, nextChainedBlock, canDownload);
                    canDownload = askAndEnqueueResult.CanDownload;
                    nextChainedBlock = askAndEnqueueResult.SubsequentChainedBlock;

                    if (askAndEnqueueResult.ShouldBreak)
                        break;
                }

                var downloadResult = await DownloadBlocks(chainedBlockStackToDownload, cancellationToken);
                if (downloadResult.ShouldBreak)
                    break;
            }

            chainedBlockStackToDownload.Clear();
            chainedBlockStackToDownload = null;

            Cleanup();

            return new BlockStoreLoopStepResult().Next();
        }

        private async Task<DownloadBlockStepResult> AskAndEnqueueBlocksToDownload(Queue<ChainedBlock> chainedBlockStackToDownload, ChainedBlock nextChainedBlock, bool canDownload)
        {
            var result = new DownloadBlockStepResult();
            result.SetCanDownload(canDownload);

            var subsequentChainedBlock = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);
            result.SubsequentChainedBlock = subsequentChainedBlock;

            var breakExecution = await ShouldBreakExecution(subsequentChainedBlock, nextChainedBlock);
            if (breakExecution && !chainedBlockStackToDownload.Any())
                return (DownloadBlockStepResult)result.Break();

            if (breakExecution && chainedBlockStackToDownload.Any())
            {
                result.SetCanDownload(false);
                return result;
            }

            this.BlockStoreLoop.StoreBlockPuller.AskBlock(subsequentChainedBlock);
            chainedBlockStackToDownload.Enqueue(subsequentChainedBlock);

            if (chainedBlockStackToDownload.Count == this.BlockStoreLoop.BatchDownloadSize)
                result.SetCanDownload(false);

            return result;
        }

        private async Task<bool> ShouldBreakExecution(ChainedBlock subsequentChainedBlock, ChainedBlock nextChainedBlock)
        {
            if (subsequentChainedBlock == null)
                return true;

            if (subsequentChainedBlock.Header.HashPrevBlock != nextChainedBlock.HashBlock)
                return true;

            if (subsequentChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return true;

            if (this.BlockStoreLoop.PendingStorage.ContainsKey(subsequentChainedBlock.HashBlock))
                return true;

            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(subsequentChainedBlock.HashBlock))
                return true;

            return false;
        }

        private async Task<BlockStoreLoopStepResult> DownloadBlocks(Queue<ChainedBlock> chainedBlocksToDownload, CancellationToken cancellationToken)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (this.BlockStoreLoop.StoreBlockPuller.TryGetBlock(chainedBlocksToDownload.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = chainedBlocksToDownload.Dequeue();
                this.blockPairsToStore.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));

                this.insertBlockSize += downloadedBlock.Length;
                this.stallCount = 0;

                // Can we push the download blocks to the block repository
                // This might go above the max insert size
                if (this.insertBlockSize > this.BlockStoreLoop.InsertBlockSizeThreshold || !chainedBlocksToDownload.Any())
                {
                    await this.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, this.blockPairsToStore.Select(t => t.Block).ToList());

                    this.BlockStoreLoop.StoredBlock = chainedBlockToDownload;
                    this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;
                    this.insertBlockSize = 0;

                    this.blockPairsToStore.Clear();

                    if (!chainedBlocksToDownload.Any())
                        return new BlockStoreLoopStepResult().Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (this.stallCount > 10000)
                    return new BlockStoreLoopStepResult().Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, cancellationToken);

                this.stallCount++;
            }

            return new BlockStoreLoopStepResult().Next();
        }

        private void Cleanup()
        {
            if (this.blockPairsToStore != null)
            {
                this.blockPairsToStore.Clear();
                this.blockPairsToStore = null;
            }
        }
    }

    internal sealed class DownloadBlockStepResult : BlockStoreLoopStepResult
    {
        internal ChainedBlock SubsequentChainedBlock { get; set; }
        internal bool CanDownload { get; private set; }

        internal void SetCanDownload(bool canDownload)
        {
            this.CanDownload = canDownload;
        }
    }
}