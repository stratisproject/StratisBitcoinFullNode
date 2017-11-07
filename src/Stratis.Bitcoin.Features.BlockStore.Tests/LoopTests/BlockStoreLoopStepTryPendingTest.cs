using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    public sealed class BlockStoreLoopStepTryPendingTest : BlockStoreLoopStepBaseTest
    {
        /// <summary>
        /// If the store is not in IBD we need to immediately push any pending blocks
        /// to the repository.
        /// </summary>
        [Fact]
        public void ProcessPendingStorage_PushToRepo_NotIBD_InMemory()
        {
            var blocks = CreateBlocks(15);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                fluent.Create(chain);

                //Set the store's tip
                fluent.Loop.SetStoreTip(fluent.Loop.Chain.GetBlock(blocks.Take(5).Last().GetHash()));

                // Add chained blocks 5 - 9 to PendingStorage
                for (int i = 5; i <= 9; i++)
                {
                    AddBlockToPendingStorage(fluent.Loop, blocks[i]);
                }

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop, this.loggerFactory);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        /// <summary>
        /// If the store IS in IBD we can process the blocks until:
        /// <list>
        /// <item>1: Pending storage count has reached <see cref="BlockStoreLoop.PendingStorageBatchThreshold"/>.</item>
        /// <item>2: A break condition is found.</item>
        /// <item>3: <see cref="BlockStoreLoop.MaxPendingInsertBlockSize"/> has been reached. </item>
        /// </list> 
        /// </summary>
        [Fact]
        public void ProcessPendingStorage_PushToRepo_IBD_InMemory()
        {
            var blocks = CreateBlocks(15);

            using (var fluent = new FluentBlockStoreLoop().AsIBD())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 15 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(14));

                // Create block store loop
                fluent.Create(chain);

                //Set the store's tip
                fluent.Loop.SetStoreTip(fluent.Loop.Chain.GetBlock(blocks.Take(5).Last().GetHash()));

                // Add chained blocks 5 - 14 to PendingStorage
                for (int i = 5; i <= 14; i++)
                {
                    AddBlockToPendingStorage(fluent.Loop, blocks[i]);
                }

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop, this.loggerFactory);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[14].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[14].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        /// <summary>
        /// If the store IS in IBD we can process the blocks until:
        /// <list>
        /// <item>1: Pending storage count has reached <see cref="BlockStoreLoop.PendingStorageBatchThreshold"/>.</item>
        /// <item>2: A break condition is found.</item>
        /// <item>3: <see cref="BlockStoreLoop.MaxPendingInsertBlockSize"/> has been reached. </item>
        /// </list> 
        /// </summary>
        [Fact]
        public void ProcessPendingStorage_PushToRepo_IBD_MaxPendingInsertBlockSize_InMemory()
        {
            var blocks = CreateBlocks(2500, true);

            using (var fluent = new FluentBlockStoreLoop().AsIBD())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 2500 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2499));

                // Create block store loop
                fluent.Create(chain);

                //Set the store's tip
                fluent.Loop.SetStoreTip(fluent.Loop.Chain.GetBlock(blocks.Take(5).Last().GetHash()));

                // Add chained blocks 5 - 2499 to PendingStorage
                for (int i = 5; i <= 2499; i++)
                {
                    AddBlockToPendingStorage(fluent.Loop, blocks[i]);
                }

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop, this.loggerFactory);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[2499].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[2499].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        /// <summary>
        /// If the store is being disposed, process and push all the blocks in pending storage
        /// </summary>
        [Fact]
        public void ProcessPendingStorage_PushToRepo_Disposing_InMemory()
        {
            var blocks = CreateBlocks(15);

            using (var fluent = new FluentBlockStoreLoop().AsIBD())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 15 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(14));

                // Create block store loop
                fluent.Create(chain);

                //Set the store's tip
                fluent.Loop.SetStoreTip(fluent.Loop.Chain.GetBlock(blocks.Take(5).Last().GetHash()));

                // Add chained blocks 5 - 14 to PendingStorage
                for (int i = 5; i <= 14; i++)
                {
                    AddBlockToPendingStorage(fluent.Loop, blocks[i]);
                }

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop, this.loggerFactory);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), true).GetAwaiter().GetResult();

                Assert.Equal(blocks[14].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[14].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }
    }
}