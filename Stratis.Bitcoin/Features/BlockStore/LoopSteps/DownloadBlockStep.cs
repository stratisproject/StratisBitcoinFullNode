using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
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

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (disposeMode)
                return BlockStoreLoopStepResult.Break();

            var downloadStack = new Queue<ChainedBlock>(new[] { nextChainedBlock });
            var store = new List<BlockPair>();
            int insertBlockSize = 0;
            int stallCount = 0;

            this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);

            var canDownload = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (canDownload)
                {
                    var inputChainedBlock = nextChainedBlock;
                    nextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(inputChainedBlock.Height + 1);

                    var breakExecution = await ShouldBreakExecution(inputChainedBlock, nextChainedBlock);
                    if (breakExecution)
                    {
                        if (!downloadStack.Any())
                            return BlockStoreLoopStepResult.Break();

                        canDownload = false;
                    }
                    else
                    {
                        this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);
                        downloadStack.Enqueue(nextChainedBlock);

                        if (downloadStack.Count == this.BlockStoreLoop.BatchDownloadSize)
                            canDownload = false;
                    }
                }

                var downloadResult = await DownloadBlocks(insertBlockSize, stallCount, store, downloadStack, cancellationToken);
                if (downloadResult.ShouldBreak)
                    break;
            }

            return BlockStoreLoopStepResult.Next();
        }

        private async Task<bool> ShouldBreakExecution(ChainedBlock inputChainedBlock, ChainedBlock nextChainedBlock)
        {
            if (nextChainedBlock == null)
                return true;

            if (nextChainedBlock.Header.HashPrevBlock != inputChainedBlock.HashBlock)
                return true;

            if (nextChainedBlock.Height > this.BlockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                return true;

            if (this.BlockStoreLoop.PendingStorage.ContainsKey(nextChainedBlock.HashBlock))
                return true;

            if (await this.BlockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
                return true;

            return false;
        }

        private async Task<BlockStoreLoopStepResult> DownloadBlocks(int insertBlockSize, int stallCount, List<BlockPair> store, Queue<ChainedBlock> chainedBlocksToDownload, CancellationToken cancellationToken)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (this.BlockStoreLoop.BlockPuller.TryGetBlock(chainedBlocksToDownload.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = chainedBlocksToDownload.Dequeue();
                store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));

                insertBlockSize += downloadedBlock.Length;
                stallCount = 0;

                // Can we push the download blocks to the block repository
                // This might go above the max insert size
                if (insertBlockSize > this.BlockStoreLoop.InsertBlockSizeThreshold || !chainedBlocksToDownload.Any())
                {
                    await this.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, store.Select(t => t.Block).ToList());

                    this.BlockStoreLoop.StoredBlock = chainedBlockToDownload;
                    this.BlockStoreLoop.ChainState.HighestPersistedBlock = this.BlockStoreLoop.StoredBlock;
                    insertBlockSize = 0;

                    store.Clear();

                    if (!chainedBlocksToDownload.Any())
                        return BlockStoreLoopStepResult.Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (stallCount > 10000)
                    return BlockStoreLoopStepResult.Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, cancellationToken);

                stallCount++;
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}