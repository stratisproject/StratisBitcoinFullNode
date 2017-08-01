using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepTryPendingTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void CanExecute_TryPending()
        {
            // Create 15 blocks
            List<Block> blocks = CreateBlocks(15);

            // The repository has 5 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\LoopTest_Pending")))
            {
                blockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter();

                var chain = new ConcurrentChain(Network.Main);

                // The chain has 10 blocks appended
                AppendBlocks(chain, blocks.Take(10));

                // Create block store loop
                BlockStoreLoop blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\LoopTest_Pending");

                // Add chained blocks 5 - 9 to PendingStorage
                AddToPendingStorage(blockStoreLoop, blocks[5]);
                AddToPendingStorage(blockStoreLoop, blocks[6]);
                AddToPendingStorage(blockStoreLoop, blocks[7]);
                AddToPendingStorage(blockStoreLoop, blocks[8]);
                AddToPendingStorage(blockStoreLoop, blocks[9]);

                //Start processing pending blocks from block 5
                ChainedBlock nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(blockStoreLoop);
                processPendingStorageStep.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), blockStoreLoop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), blockStoreLoop.StoreTip.HashBlock);
            }
        }
    }
}