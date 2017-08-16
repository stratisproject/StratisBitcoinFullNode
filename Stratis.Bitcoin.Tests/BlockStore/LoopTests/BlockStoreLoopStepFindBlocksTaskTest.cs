using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepFindBlocksTaskTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void BlockStoreInnerStepFindBlocks_WithBlocksFound_AddToDownloadStack()
        {
            var blocks = CreateBlocks(10);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                fluent.Create(chain);

                // Push blocks 5 - 9 to the downloaded blocks collection
                fluent.Loop.BlockPuller.InjectBlock(blocks[5].GetHash(), new DownloadedBlock() { Length = blocks[5].GetSerializedSize(), Block = blocks[5] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[6].GetHash(), new DownloadedBlock() { Length = blocks[6].GetSerializedSize(), Block = blocks[6] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[7].GetHash(), new DownloadedBlock() { Length = blocks[7].GetSerializedSize(), Block = blocks[7] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[8].GetHash(), new DownloadedBlock() { Length = blocks[8].GetSerializedSize(), Block = blocks[8] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[9].GetHash(), new DownloadedBlock() { Length = blocks[9].GetSerializedSize(), Block = blocks[9] }, new CancellationToken());

                //Start processing blocks to download from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //Block[5] and Block[6] in the DownloadStack
                Assert.Equal(2, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[5].GetHash()));
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[6].GetHash()));
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_BatchDownloadSizeReached()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 3 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks.Take(2).ToList()).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2).ToList());

                // Create block store loop
                fluent.Create(chain);
                fluent.Loop.BatchDownloadSize = 2;

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //Block[1] and Block[2] in the DownloadStack
                Assert.Equal(2, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[1].GetHash()));
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[2].GetHash()));

                //The FindBlocks() task should be removed from the routine
                //as the batch download size is reached
                Assert.Equal(1, context.InnerSteps.Count());
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_BlockExistsInPendingStorage()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 3 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                //Add nextChainedBlock to Pending Storage
                fluent.Loop.PendingStorage.TryAdd(blocks[2].GetHash(), new BlockPair(blocks[2], nextChainedBlock));

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block exists in PendingStorage
                Assert.Equal(1, context.InnerSteps.Count());
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_BlockExistsInRepository()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 3 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block exist in the BlockRepository
                //causing a stop condition
                Assert.Equal(1, context.InnerSteps.Count());
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_NextChainedBlockIsNull()
        {
            var blocks = CreateBlocks(2);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 2 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(1));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block is null
                Assert.Equal(1, context.InnerSteps.Count());
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanBreakExecution_DownloadStackIsEmpty()
        {
            var blocks = CreateBlocks(2);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 2 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(1));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);
                context.DownloadStack.Clear();

                var task = new BlockStoreInnerStepFindBlocks();
                var result = task.ExecuteAsync(context).GetAwaiter().GetResult();
                Assert.Equal(InnerStepResult.Stop, result);
            }
        }
    }
}