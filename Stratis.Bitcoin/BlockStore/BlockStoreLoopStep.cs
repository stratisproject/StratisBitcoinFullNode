using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
    internal sealed class BlockStoreLoopStepChain
    {
        private List<BlockStoreLoopStep> steps = new List<BlockStoreLoopStep>();
        private BlockStoreLoop blockStoreLoop;
        private ChainedBlock nextChainedBlock;
        private bool disposeMode;
        private CancellationToken cancellationToken;

        public BlockStoreLoopStepChain(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            this.blockStoreLoop = blockStoreLoop;
            this.nextChainedBlock = nextChainedBlock;
            this.cancellationToken = cancellationToken;
            this.disposeMode = disposeMode;
        }

        internal void SetNextStep(BlockStoreLoopStep step)
        {
            this.steps.Add(step);
        }

        internal async Task<BlockStoreLoopStepResult> Execute()
        {
            BlockStoreLoopStepResult result = null;

            foreach (var step in this.steps)
            {
                var stepResult = await step.Execute(this.blockStoreLoop, this.nextChainedBlock, this.cancellationToken, this.disposeMode);

                if (stepResult.ShouldBreak)
                {
                    result = stepResult;
                    break;
                }

                if (result.ShouldContinue)
                {
                    result = stepResult;
                    break;
                }
            }

            return result;
        }
    }

    internal abstract class BlockStoreLoopStep
    {
        internal abstract Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    internal sealed class BlockStoreLoopStepResult
    {
        private BlockStoreLoopStepResult() { }

        internal bool ShouldBreak { get; private set; }
        internal bool ShouldContinue { get; private set; }

        internal static BlockStoreLoopStepResult Break()
        {
            return new BlockStoreLoopStepResult() { ShouldBreak = true };
        }

        internal static BlockStoreLoopStepResult Continue()
        {
            return new BlockStoreLoopStepResult() { ShouldContinue = true };
        }

        internal static BlockStoreLoopStepResult Next()
        {
            return new BlockStoreLoopStepResult();
        }
    }

    internal sealed class BlockStoreLoopStepReorganise : BlockStoreLoopStep
    {
        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (blockStoreLoop.StoredBlock.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return BlockStoreLoopStepResult.Break();

                var blocksToDelete = new List<uint256>();
                var delete = blockStoreLoop.StoredBlock;

                //The chained block does not exist on the chain
                //Add blocks to delete to the blocksToDelete collection by walking back the chain until the last chained block is found
                while (blockStoreLoop.Chain.GetBlock(delete.HashBlock) == null)
                {
                    blocksToDelete.Add(delete.HashBlock);
                    delete = delete.Previous;
                }

                //Delete the un-persisted blocks from the repository
                await blockStoreLoop.BlockRepository.DeleteAsync(delete.HashBlock, blocksToDelete);

                //Set the last stored block to the last found chained block
                blockStoreLoop.StoredBlock = delete;
                blockStoreLoop.ChainState.HighestPersistedBlock = blockStoreLoop.StoredBlock;

                return BlockStoreLoopStepResult.Break();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepCheckExists : BlockStoreLoopStep
    {
        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (await blockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
            {
                // next block is in storage update StoredBlock 
                await blockStoreLoop.BlockRepository.SetBlockHash(nextChainedBlock.HashBlock);
                blockStoreLoop.StoredBlock = nextChainedBlock;
                blockStoreLoop.ChainState.HighestPersistedBlock = blockStoreLoop.StoredBlock;
                return BlockStoreLoopStepResult.Continue();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepTryPending : BlockStoreLoopStep
    {
        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            // check if the next block is in pending storage
            // then loop over the pending items and push to store in batches
            // if a stop condition is met break from the loop back to the start

            BlockPair insert;

            if (blockStoreLoop.PendingStorage.TryGetValue(nextChainedBlock.HashBlock, out insert))
            {
                // if in IBD and batch is not full then wait for more blocks
                if (blockStoreLoop.ChainState.IsInitialBlockDownload && !disposeMode)
                    if (blockStoreLoop.PendingStorage.Skip(0).Count() < blockStoreLoop.batchtriggersize) // ConcurrentDictionary perf
                        return BlockStoreLoopStepResult.Break();

                if (!blockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out insert))
                    return BlockStoreLoopStepResult.Break();

                var tostore = new List<BlockPair>(new[] { insert });
                var storebest = nextChainedBlock;
                var insertSize = insert.Block.GetSerializedSize();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var old = nextChainedBlock;
                    nextChainedBlock = blockStoreLoop.Chain.GetBlock(nextChainedBlock.Height + 1);

                    var stop = false;
                    // stop if at the tip or block is already in store or pending insertion
                    if (nextChainedBlock == null)
                        stop = true;
                    else if (nextChainedBlock.Header.HashPrevBlock != old.HashBlock)
                        stop = true;
                    else if (nextChainedBlock.Height > blockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                        stop = true;
                    else if (!blockStoreLoop.PendingStorage.TryRemove(nextChainedBlock.HashBlock, out insert))
                        stop = true;

                    if (stop)
                    {
                        if (!tostore.Any())
                            return BlockStoreLoopStepResult.Break();
                    }
                    else
                    {
                        tostore.Add(insert);
                        storebest = nextChainedBlock;
                        insertSize += insert.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler	
                    }

                    if (insertSize > blockStoreLoop.insertsizebyte || stop)
                    {
                        // store missing blocks and remove them from pending blocks
                        await blockStoreLoop.BlockRepository.PutAsync(storebest.HashBlock, tostore.Select(b => b.Block).ToList());
                        blockStoreLoop.StoredBlock = storebest;
                        blockStoreLoop.ChainState.HighestPersistedBlock = blockStoreLoop.StoredBlock;

                        if (stop)
                            return BlockStoreLoopStepResult.Break();

                        tostore.Clear();
                        insertSize = 0;

                        // this can be twicked if insert is effecting the consensus speed
                        if (blockStoreLoop.ChainState.IsInitialBlockDownload)
                            await Task.Delay(blockStoreLoop.pushIntervalIBD, cancellationToken);
                    }
                }

                return BlockStoreLoopStepResult.Continue();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepTryDownload : BlockStoreLoopStep
    {
        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode)
        {
            if (disposeMode)
                return BlockStoreLoopStepResult.Break();

            // continuously download blocks until a stop condition is found.
            // there are two operations, one is finding blocks to download 
            // and asking them to the puller and the other is collecting
            // downloaded blocks and persisting them as a batch.
            var store = new List<BlockPair>();
            var downloadStack = new Queue<ChainedBlock>(new[] { nextChainedBlock });
            blockStoreLoop.blockPuller.AskBlock(nextChainedBlock);

            int insertDownloadSize = 0;
            int stallCount = 0;
            bool download = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (download)
                {
                    var old = nextChainedBlock;
                    nextChainedBlock = blockStoreLoop.Chain.GetBlock(old.Height + 1);

                    var stop = false;
                    // stop if at the tip or block is already in store or pending insertion
                    if (nextChainedBlock == null) stop = true;
                    else if (nextChainedBlock.Header.HashPrevBlock != old.HashBlock)
                        stop = true;
                    else if (nextChainedBlock.Height > blockStoreLoop.ChainState.HighestValidatedPoW?.Height)
                        stop = true;
                    else if (blockStoreLoop.PendingStorage.ContainsKey(nextChainedBlock.HashBlock))
                        stop = true;
                    else if (await blockStoreLoop.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
                        stop = true;

                    if (stop)
                    {
                        if (!downloadStack.Any())
                            return BlockStoreLoopStepResult.Break();

                        download = false;
                    }
                    else
                    {
                        blockStoreLoop.blockPuller.AskBlock(nextChainedBlock);
                        downloadStack.Enqueue(nextChainedBlock);

                        if (downloadStack.Count == blockStoreLoop.batchdownloadsize)
                            download = false;
                    }
                }

                BlockPuller.DownloadedBlock block;

                if (blockStoreLoop.blockPuller.TryGetBlock(downloadStack.Peek(), out block))
                {
                    var downloadbest = downloadStack.Dequeue();
                    store.Add(new BlockPair { Block = block.Block, ChainedBlock = downloadbest });
                    insertDownloadSize += block.Length;
                    stallCount = 0;

                    // can we push
                    if (insertDownloadSize > blockStoreLoop.insertsizebyte || !downloadStack.Any()) // this might go above the max insert size
                    {
                        await blockStoreLoop.BlockRepository.PutAsync(downloadbest.HashBlock, store.Select(t => t.Block).ToList());
                        blockStoreLoop.StoredBlock = downloadbest;
                        blockStoreLoop.ChainState.HighestPersistedBlock = blockStoreLoop.StoredBlock;
                        insertDownloadSize = 0;
                        store.Clear();

                        if (!downloadStack.Any())
                            return BlockStoreLoopStepResult.Break();
                    }
                }
                else
                {
                    // if a block is stalled or lost to the downloader 
                    // this will make sure the loop start again after a threshold
                    if (stallCount > 10000)
                        return BlockStoreLoopStepResult.Break();

                    // waiting for blocks so sleep 100 ms
                    await Task.Delay(100, cancellationToken);
                    stallCount++;
                }
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}