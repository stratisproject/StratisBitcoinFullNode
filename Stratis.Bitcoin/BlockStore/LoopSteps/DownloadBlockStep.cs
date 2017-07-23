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
        internal DownloadBlockStep(BlockStoreLoop blockStoreLoop, CancellationToken cancellationToken)
            : base(blockStoreLoop, cancellationToken)
        {
        }

        private int insertBlockSize = 0;
        private int stallCount = 0;

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            if (disposeMode)
                return BlockStoreLoopStepResult.Break();

            var chainedBlockStackToDownload = new Queue<ChainedBlock>(new[] { nextChainedBlock });

            this.BlockStoreLoop.blockPuller.AskBlock(nextChainedBlock);

            var canDownload = true;

            while (!this.CancellationToken.IsCancellationRequested)
            {
                if (canDownload)
                {
                    var askAndEnqueueResult = await AskAndEnqueueBlocksToDownload(chainedBlockStackToDownload, nextChainedBlock, canDownload);
                    if (askAndEnqueueResult.ShouldBreak)
                        break;
                }

                var downloadResult = await DownloadBlocks(chainedBlockStackToDownload);
                if (downloadResult.ShouldBreak)
                    break;
            }

            return BlockStoreLoopStepResult.Next();
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

        private async Task<BlockStoreLoopStepResult> AskAndEnqueueBlocksToDownload(Queue<ChainedBlock> chainedBlockStackToDownload, ChainedBlock nextChainedBlock, bool canDownload)
        {
            var chainedBlockToProcess = this.BlockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

            var breakExecution = await ShouldBreakExecution(chainedBlockToProcess, nextChainedBlock);

            if (breakExecution && chainedBlockStackToDownload.Count == 0)
                return BlockStoreLoopStepResult.Break();

            if (breakExecution && chainedBlockStackToDownload.Count > 0)
                canDownload = false;

            this.BlockStoreLoop.blockPuller.AskBlock(chainedBlockToProcess);
            chainedBlockStackToDownload.Enqueue(chainedBlockToProcess);

            if (chainedBlockStackToDownload.Count == this.BlockStoreLoop.BatchDownloadSize)
                canDownload = false;

            return BlockStoreLoopStepResult.Next();
        }

        private async Task<BlockStoreLoopStepResult> DownloadBlocks(Queue<ChainedBlock> chainedBlockStackToDownload)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            var blockPairsToStore = new List<BlockPair>();

            if (this.BlockStoreLoop.blockPuller.TryGetBlock(chainedBlockStackToDownload.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = chainedBlockStackToDownload.Dequeue();
                blockPairsToStore.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));

                this.insertBlockSize += downloadedBlock.Length;
                this.stallCount = 0;

                // Can we push the download blocks to the block repository
                // This might go above the max insert size
                if (this.insertBlockSize > this.BlockStoreLoop.InsertBlockSizeThreshold || !chainedBlockStackToDownload.Any())
                {
                    await this.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, blockPairsToStore.Select(t => t.Block).ToList());

                    this.BlockStoreLoop.StoredBlock = chainedBlockToDownload;
                    this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;
                    this.insertBlockSize = 0;

                    blockPairsToStore.Clear();
                    blockPairsToStore = null;

                    if (!chainedBlockStackToDownload.Any())
                        return BlockStoreLoopStepResult.Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (this.stallCount > 10000)
                    return BlockStoreLoopStepResult.Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, this.CancellationToken);

                this.stallCount++;
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}