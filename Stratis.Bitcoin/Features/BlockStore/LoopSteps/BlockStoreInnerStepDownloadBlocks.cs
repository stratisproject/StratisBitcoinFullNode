using Stratis.Bitcoin.BlockPulling;
using System.Linq; 
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Downloads blocks from the BlockPuller removes block from the DownloadStack.
    /// <para>
    /// If the block exists in the puller add the the downloaded block to the store to
    /// push to the repository. If the <see cref="BlockStoreLoop.InsertBlockSizeThreshold"/> has been reached
    /// push the blocks in the context's Store to the repository.
    /// </para> 
    /// <para>
    /// When the download stack is empty return a Break() result causing the BlockStoreLoop to
    /// start again.
    /// </para>
    /// <para>
    /// If a block is stalled or lost to the downloader, start again after a threshold <see cref="BlockStoreLoop.StallCount"/>
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepDownloadBlocks : BlockStoreInnerStep
    {
        /// <inheritdoc/>
        public override async Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context)
        {
            BlockPuller.DownloadedBlock downloadedBlock;

            if (context.BlockStoreLoop.BlockPuller.TryGetBlock(context.DownloadStack.Peek(), out downloadedBlock))
            {
                var chainedBlockToDownload = context.DownloadStack.Dequeue();
                context.Store.Add(new BlockPair(downloadedBlock.Block, chainedBlockToDownload));
                context.InsertBlockSize += downloadedBlock.Length;
                context.StallCount = 0;

                if (context.InsertBlockSize > context.BlockStoreLoop.InsertBlockSizeThreshold || !context.DownloadStack.Any())
                {
                    var blocksToStore = context.Store.Select(bp => bp.Block).ToList();
                    await context.BlockStoreLoop.BlockRepository.PutAsync(chainedBlockToDownload.HashBlock, blocksToStore);
                    context.BlockStoreLoop.SetStoreTip(chainedBlockToDownload);
                    context.InsertBlockSize = 0;
                    context.Store.Clear();

                    if (!context.DownloadStack.Any())
                        return InnerStepResult.Stop;
                }
            }
            else
            {
                if (context.StallCount > 10000)
                    return InnerStepResult.Stop;

                await Task.Delay(100, context.CancellationToken);

                context.StallCount++;
            }

            return InnerStepResult.Next;
        }
    }
}