using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Downloads blocks from the BlockPuller and 
    /// removes block from the DownloadStack
    /// 
    /// Once the downloadStack is empty or InsertBlockSizeThreshold has been reached
    /// the blocks in the store will be pushed to the Repository
    /// </summary>
    public sealed class BlockStoreStepDownloadBlocksTask : BlockStoreStepTask
    {
        public override async Task<BlockStoreLoopStepResult> ExecuteAsync(BlockStoreStepTaskContext context)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (context.BlockStoreLoop.BlockPuller.TryGetBlock(context.DownloadStack.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = context.DownloadStack.Dequeue();
                context.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));
                context.InsertBlockSize += downloadedBlock.Length;
                context.StallCount = 0;

                // Can we push the download blocks to the block repository
                // This might go above the max insert size
                if (context.InsertBlockSize > context.BlockStoreLoop.InsertBlockSizeThreshold || !context.DownloadStack.Any())
                {
                    var blocksToStore = context.Store.Select(bp => bp.Block).ToList();
                    await context.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, blocksToStore);
                    context.BlockStoreLoop.SetStoreTip(chainedBlockToDownload);
                    context.InsertBlockSize = 0;
                    context.Store.Clear();

                    if (!context.DownloadStack.Any())
                        return BlockStoreLoopStepResult.Break();
                }
            }
            else
            {
                // If a block is stalled or lost to the downloader this will make that sure the loop starts again after a threshold
                if (context.StallCount > 10000)
                    return BlockStoreLoopStepResult.Break();

                // Waiting for blocks so sleep 100 ms
                await Task.Delay(100, context.CancellationToken);

                context.StallCount++;
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}