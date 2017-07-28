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

        private int insertBlockSize = 0;
        private int stallCount = 0;
        private List<BlockPair> blockPairsToStore = new List<BlockPair>();

        internal override async Task<BlockStoreLoopStepResult> Execute(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (disposeMode)
            {
                Cleanup();
                return BlockStoreLoopStepResult.Break();
            }

            var chainedBlockStackToDownload = new Queue<ChainedBlock>(new[] { nextChainedBlock });

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
                        if (!chainedBlockStackToDownload.Any())
                            return BlockStoreLoopStepResult.Break();

                        canDownload = false;
                    }
                    else
                    {
                        this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);
                        chainedBlockStackToDownload.Enqueue(nextChainedBlock);

                        if (chainedBlockStackToDownload.Count == this.BlockStoreLoop.BatchDownloadSize)
                            canDownload = false;
                    }
                }

                var downloadResult = await DownloadBlocks(chainedBlockStackToDownload, cancellationToken);
                if (downloadResult.ShouldBreak)
                    break;
            }

            chainedBlockStackToDownload.Clear();
            chainedBlockStackToDownload = null;

            Cleanup();

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

        private async Task<BlockStoreLoopStepResult> DownloadBlocks(Queue<ChainedBlock> chainedBlocksToDownload, CancellationToken cancellationToken)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (this.BlockStoreLoop.BlockPuller.TryGetBlock(chainedBlocksToDownload.Peek(), out downloadedBlock))
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
                        return BlockStoreLoopStepResult.Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (this.stallCount > 10000)
                    return BlockStoreLoopStepResult.Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, cancellationToken);

                this.stallCount++;
            }

            return BlockStoreLoopStepResult.Next();
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
}