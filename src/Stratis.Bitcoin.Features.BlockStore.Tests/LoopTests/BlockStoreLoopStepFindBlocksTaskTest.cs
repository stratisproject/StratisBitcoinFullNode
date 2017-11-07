using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
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

                // Push blocks[5] - [9] to the downloaded blocks collection
                for (int i = 5; i <= 9; i++)
                {
                    fluent.Loop.BlockPuller.InjectBlock(blocks[i].GetHash(), new DownloadedBlock { Length = blocks[i].GetSerializedSize(), Block = blocks[i] }, new CancellationToken());
                }

                // Start finding blocks from block[5]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);

                var task = new BlockStoreInnerStepFindBlocks(this.loggerFactory);
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                // Block[5] through Block[9] should be in the DownloadStack
                Assert.Equal(5, context.DownloadStack.Count());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[5].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[6].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[7].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[8].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[9].GetHash());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_DownloadStackSizeReached()
        {
            var blocks = CreateBlocks(55);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 45 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(45).Last().GetHash(), blocks.Take(45).ToList()).GetAwaiter().GetResult();

                // The chain has 55 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(54).ToList());

                // Create block store loop
                fluent.Create(chain);

                // Start finding blocks from Block[45]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[45].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);

                var task = new BlockStoreInnerStepFindBlocks(this.loggerFactory);
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                // Block[45] through Block[50] should be in the DownloadStack
                Assert.Equal(10, context.DownloadStack.Count());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[45].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[46].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[47].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[48].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[49].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[50].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[51].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[52].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[53].GetHash());
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == blocks[54].GetHash());

                // The FindBlocks() task should be removed from the routine
                // as the batch download size is reached
                Assert.Single(context.InnerSteps);
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_BlockExistsInPendingStorage()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 2 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(1).Last().GetHash(), blocks.Take(1).ToList()).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                // Add block[2] to Pending Storage
                fluent.Loop.PendingStorage.TryAdd(blocks[2].GetHash(), new BlockPair(blocks[2], fluent.Loop.Chain.GetBlock(blocks[2].GetHash())));

                // Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);

                var task = new BlockStoreInnerStepFindBlocks(this.loggerFactory);
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                // DownloadStack should only contain Block[1]
                Assert.Single(context.DownloadStack);
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == nextChainedBlock.HashBlock);

                // The FindBlocks() task should be removed from the routine
                // as the next chained block exists in PendingStorage
                Assert.Single(context.InnerSteps);
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

                // Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);

                var task = new BlockStoreInnerStepFindBlocks(this.loggerFactory);
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                // DownloadStack should only contain Block[1]
                Assert.Empty(context.DownloadStack);

                // The FindBlocks() task should be removed from the routine
                // as the next chained block exist in the BlockRepository
                // causing a stop condition
                Assert.Single(context.InnerSteps);
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }

        [Fact]
        public void BlockStoreInnerStepFindBlocks_CanRemoveTaskFromRoutine_NextChainedBlockIsNull()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 2 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(2).Last().GetHash(), blocks.Take(2).ToList()).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                // Start finding blocks from Block[2]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[2].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);

                var task = new BlockStoreInnerStepFindBlocks(this.loggerFactory);
                task.ExecuteAsync(context).GetAwaiter().GetResult();

                // DownloadStack should only contain nextChainedBlock
                Assert.Single(context.DownloadStack);
                Assert.Contains(context.DownloadStack, cb => cb.HashBlock == nextChainedBlock.HashBlock);

                // The FindBlocks() task should be removed from the routine
                // as the next chained block is null
                Assert.Single(context.InnerSteps);
                Assert.False(context.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().Any());
            }
        }
    }
}