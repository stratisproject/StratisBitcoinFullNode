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
    //      Download blocks and persisting them as a batch to the BlockRepository

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

            var stepContext = new DownloadBlockStepContext(nextChainedBlock);

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
                        if (!stepContext.DownloadStack.Any())
                            return BlockStoreLoopStepResult.Break();

                        canDownload = false;
                    }
                    else
                    {
                        this.BlockStoreLoop.BlockPuller.AskBlock(nextChainedBlock);
                        stepContext.DownloadStack.Enqueue(nextChainedBlock);

                        if (stepContext.DownloadStack.Count == this.BlockStoreLoop.BatchDownloadSize)
                            canDownload = false;
                    }
                }

                var downloadResult = await DownloadBlocks(stepContext, cancellationToken);
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

        private async Task<BlockStoreLoopStepResult> DownloadBlocks(DownloadBlockStepContext stepContext, CancellationToken cancellationToken)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (this.BlockStoreLoop.BlockPuller.TryGetBlock(stepContext.DownloadStack.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = stepContext.DownloadStack.Dequeue();
                stepContext.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));

                stepContext.InsertBlockSize += downloadedBlock.Length;
                stepContext.StallCount = 0;

                // Can we push the download blocks to the block repository
                // This might go above the max insert size
                if (stepContext.InsertBlockSize > this.BlockStoreLoop.InsertBlockSizeThreshold || !stepContext.DownloadStack.Any())
                {
                    await this.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, stepContext.Store.Select(t => t.Block).ToList());

                    this.BlockStoreLoop.SetStoreTip(chainedBlockToDownload);

                    stepContext.InsertBlockSize = 0;
                    stepContext.Store.Clear();

                    if (!stepContext.DownloadStack.Any())
                        return BlockStoreLoopStepResult.Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (stepContext.StallCount > 10000)
                    return BlockStoreLoopStepResult.Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, cancellationToken);

                stepContext.StallCount++;
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class DownloadBlockStepContext
    {
        internal Queue<ChainedBlock> DownloadStack;
        internal List<BlockPair> Store;
        internal int InsertBlockSize;
        internal int StallCount;

        internal DownloadBlockStepContext(ChainedBlock nextChainedBlock)
        {
            this.DownloadStack = new Queue<ChainedBlock>(new[] { nextChainedBlock });
            this.Store = new List<BlockPair>();
            this.InsertBlockSize = 0;
            this.StallCount = 0;
        }
    }
}