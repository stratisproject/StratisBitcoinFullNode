using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
    internal abstract class BlockStoreLoopStep
    {
        protected BlockStoreLoopStep(ConcurrentChain chain, CancellationToken token)
        {
            this.chain = chain;
            this.token = token;
        }

        internal readonly ConcurrentChain chain;
        internal CancellationToken token;

        internal abstract Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop loop, ChainedBlock next, bool disposeMode);
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
        internal BlockStoreLoopStepReorganise(ConcurrentChain chain, CancellationToken token)
            : base(chain, token)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop loop, ChainedBlock next, bool disposeMode)
        {
            // reorg logic
            if (loop.StoredBlock.HashBlock != next.Header.HashPrevBlock)
            {
                if (disposeMode)
                    return BlockStoreLoopStepResult.Break();

                var blockstoremove = new List<uint256>();
                var remove = loop.StoredBlock;
                // reorg - we need to delete blocks, start walking back the chain
                while (this.chain.GetBlock(remove.HashBlock) == null)
                {
                    blockstoremove.Add(remove.HashBlock);
                    remove = remove.Previous;
                }

                await loop.BlockRepository.DeleteAsync(remove.HashBlock, blockstoremove);
                loop.StoredBlock = remove;
                loop.ChainState.HighestPersistedBlock = loop.StoredBlock;

                return BlockStoreLoopStepResult.Break();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepCheckExists : BlockStoreLoopStep
    {
        public BlockStoreLoopStepCheckExists(ConcurrentChain chain, CancellationToken token)
            : base(chain, token)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop loop, ChainedBlock next, bool disposeMode)
        {
            if (await loop.BlockRepository.ExistAsync(next.HashBlock))
            {
                // next block is in storage update StoredBlock 
                await loop.BlockRepository.SetBlockHash(next.HashBlock);
                loop.StoredBlock = next;
                loop.ChainState.HighestPersistedBlock = loop.StoredBlock;
                return BlockStoreLoopStepResult.Continue();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepTryPending : BlockStoreLoopStep
    {
        public BlockStoreLoopStepTryPending(ConcurrentChain chain, CancellationToken token)
            : base(chain, token)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop loop, ChainedBlock next, bool disposeMode)
        {
            // check if the next block is in pending storage
            // then loop over the pending items and push to store in batches
            // if a stop condition is met break from the loop back to the start

            BlockPair insert;

            if (loop.PendingStorage.TryGetValue(next.HashBlock, out insert))
            {
                // if in IBD and batch is not full then wait for more blocks
                if (loop.ChainState.IsInitialBlockDownload && !disposeMode)
                    if (loop.PendingStorage.Skip(0).Count() < loop.batchtriggersize) // ConcurrentDictionary perf
                        return BlockStoreLoopStepResult.Break();

                if (!loop.PendingStorage.TryRemove(next.HashBlock, out insert))
                    return BlockStoreLoopStepResult.Break();

                var tostore = new List<BlockPair>(new[] { insert });
                var storebest = next;
                var insertSize = insert.Block.GetSerializedSize();
                while (!this.token.IsCancellationRequested)
                {
                    var old = next;
                    next = this.chain.GetBlock(next.Height + 1);

                    var stop = false;
                    // stop if at the tip or block is already in store or pending insertion
                    if (next == null) stop = true;
                    else if (next.Header.HashPrevBlock != old.HashBlock) stop = true;
                    else if (next.Height > loop.ChainState.HighestValidatedPoW?.Height) stop = true;
                    else if (!loop.PendingStorage.TryRemove(next.HashBlock, out insert)) stop = true;

                    if (stop)
                    {
                        if (!tostore.Any())
                            return BlockStoreLoopStepResult.Break();
                    }
                    else
                    {
                        tostore.Add(insert);
                        storebest = next;
                        insertSize += insert.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler	
                    }

                    if (insertSize > loop.insertsizebyte || stop)
                    {
                        // store missing blocks and remove them from pending blocks
                        await loop.BlockRepository.PutAsync(storebest.HashBlock, tostore.Select(b => b.Block).ToList());
                        loop.StoredBlock = storebest;
                        loop.ChainState.HighestPersistedBlock = loop.StoredBlock;

                        if (stop)
                            return BlockStoreLoopStepResult.Break();

                        tostore.Clear();
                        insertSize = 0;

                        // this can be twicked if insert is effecting the consensus speed
                        if (loop.ChainState.IsInitialBlockDownload)
                            await Task.Delay(loop.pushIntervalIBD, this.token);
                    }
                }

                return BlockStoreLoopStepResult.Continue();
            }

            return BlockStoreLoopStepResult.Next();
        }
    }

    internal sealed class BlockStoreLoopStepTryDownload : BlockStoreLoopStep
    {
        public BlockStoreLoopStepTryDownload(ConcurrentChain chain, CancellationToken token)
            : base(chain, token)
        {
        }

        internal override async Task<BlockStoreLoopStepResult> Execute(BlockStoreLoop loop, ChainedBlock next, bool disposeMode)
        {
            // continuously download blocks until a stop condition is found.
            // there are two operations, one is finding blocks to download 
            // and asking them to the puller and the other is collecting
            // downloaded blocks and persisting them as a batch.
            var store = new List<BlockPair>();
            var downloadStack = new Queue<ChainedBlock>(new[] { next });
            loop.blockPuller.AskBlock(next);

            int insertDownloadSize = 0;
            int stallCount = 0;
            bool download = true;

            while (!this.token.IsCancellationRequested)
            {
                if (download)
                {
                    var old = next;
                    next = this.chain.GetBlock(old.Height + 1);

                    var stop = false;
                    // stop if at the tip or block is already in store or pending insertion
                    if (next == null) stop = true;
                    else if (next.Header.HashPrevBlock != old.HashBlock)
                        stop = true;
                    else if (next.Height > loop.ChainState.HighestValidatedPoW?.Height)
                        stop = true;
                    else if (loop.PendingStorage.ContainsKey(next.HashBlock))
                        stop = true;
                    else if (await loop.BlockRepository.ExistAsync(next.HashBlock))
                        stop = true;

                    if (stop)
                    {
                        if (!downloadStack.Any())
                            return BlockStoreLoopStepResult.Break();

                        download = false;
                    }
                    else
                    {
                        loop.blockPuller.AskBlock(next);
                        downloadStack.Enqueue(next);

                        if (downloadStack.Count == loop.batchdownloadsize)
                            download = false;
                    }
                }

                BlockPuller.DownloadedBlock block;

                if (loop.blockPuller.TryGetBlock(downloadStack.Peek(), out block))
                {
                    var downloadbest = downloadStack.Dequeue();
                    store.Add(new BlockPair { Block = block.Block, ChainedBlock = downloadbest });
                    insertDownloadSize += block.Length;
                    stallCount = 0;

                    // can we push
                    if (insertDownloadSize > loop.insertsizebyte || !downloadStack.Any()) // this might go above the max insert size
                    {
                        await loop.BlockRepository.PutAsync(downloadbest.HashBlock, store.Select(t => t.Block).ToList());
                        loop.StoredBlock = downloadbest;
                        loop.ChainState.HighestPersistedBlock = loop.StoredBlock;
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
                    await Task.Delay(100, this.token);
                    stallCount++;
                }
            }

            return BlockStoreLoopStepResult.Next();
        }
    }
}
