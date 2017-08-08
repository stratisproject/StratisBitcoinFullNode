using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepFindBlocksTaskTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void FindBlocks_CanFind()
        {
            // Create 10 blocks
            List<Block> blocks = CreateBlocks(10);

            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanFind")))
            {
                // Push 5 blocks to the repository
                blockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanFind");

                // Push blocks 5 - 9 to the downloaded blocks collection
                blockStoreLoop.BlockPuller.InjectBlock(blocks[5].GetHash(), new DownloadedBlock() { Length = blocks[5].GetSerializedSize(), Block = blocks[5] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[6].GetHash(), new DownloadedBlock() { Length = blocks[6].GetSerializedSize(), Block = blocks[6] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[7].GetHash(), new DownloadedBlock() { Length = blocks[7].GetSerializedSize(), Block = blocks[7] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[8].GetHash(), new DownloadedBlock() { Length = blocks[8].GetSerializedSize(), Block = blocks[8] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[9].GetHash(), new DownloadedBlock() { Length = blocks[9].GetSerializedSize(), Block = blocks[9] }, new CancellationToken());

                //Start processing blocks to download from block 5
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[5].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //Block[5] and Block[6] in the DownloadStack
                Assert.Equal(2, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[5].GetHash()));
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[6].GetHash()));
            }
        }

        [Fact]
        public void FindBlocks_CanRemoveTaskFromRoutine_BatchDownloadSizeReached()
        {
            // Create 3 blocks
            List<Block> blocks = CreateBlocks(3);

            // The repository has 3 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BatchDownloadSizeReached")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks.Take(2).ToList()).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(2).ToList());

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BatchDownloadSizeReached");
                blockStoreLoop.BatchDownloadSize = 2;

                //Start finding blocks from Block[1]
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //Block[1] and Block[2] in the DownloadStack
                Assert.Equal(2, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[1].GetHash()));
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == blocks[2].GetHash()));

                //The FindBlocks() task should be removed from the routine
                //as the batch download size is reached
                Assert.Equal(1, context.Routine.Count());
                Assert.False(context.Routine.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInPendingStorage()
        {
            // Create 3 blocks
            List<Block> blocks = CreateBlocks(3);

            // The repository has 3 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInPendingStorage")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInPendingStorage");

                //Start finding blocks from Block[1]
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[1].GetHash());

                //Add nextChainedBlock to Pending Storage
                blockStoreLoop.PendingStorage.TryAdd(blocks[2].GetHash(), new BlockPair(blocks[2], nextChainedBlock));

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block exists in PendingStorage
                Assert.Equal(1, context.Routine.Count());
                Assert.False(context.Routine.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInRepository()
        {
            // Create 3 blocks
            List<Block> blocks = CreateBlocks(3);

            // The repository has 3 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInRepository")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_BlockExistsInRepository");

                //Start finding blocks from Block[1]
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block exist in the BlockRepository
                //causing a stop condition
                Assert.Equal(1, context.Routine.Count());
                Assert.False(context.Routine.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void FindBlocks_CanRemoveTaskFromRoutine_NextChainedBlockIsNull()
        {
            // Create 2 blocks
            List<Block> blocks = CreateBlocks(2);

            // The repository has 2 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_NextChainedBlockIsNull")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(1));

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanRemoveTaskFromRoutine_NextChainedBlockIsNull");

                //Start finding blocks from Block[1]
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);

                var task = new BlockStoreInnerStepFindBlocks();
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                //DownloadStack should only contain nextChainedBlock
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                //The FindBlocks() task should be removed from the routine
                //as the next chained block is null
                Assert.Equal(1, context.Routine.Count());
                Assert.False(context.Routine.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void FindBlocks_CanBreakExecution_DownloadStackIsEmpty()
        {
            // Create 2 blocks
            List<Block> blocks = CreateBlocks(2);

            // The repository has 2 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\FindBlocks_CanBreakExecution_DownloadStackIsEmpty")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(1));

                // Create block store loop
                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\FindBlocks_CanBreakExecution_DownloadStackIsEmpty");

                //Start finding blocks from Block[1]
                var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), blockStoreLoop).Initialize(nextChainedBlock);
                context.DownloadStack.Clear();

                var task = new BlockStoreInnerStepFindBlocks();
                var result = task.ExecuteAsync(context).GetAwaiter().GetResult();
                Assert.True(result.ShouldBreak);
            }
        }
    }
}