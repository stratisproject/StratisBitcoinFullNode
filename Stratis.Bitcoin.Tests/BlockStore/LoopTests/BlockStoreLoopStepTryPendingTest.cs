using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepTryPendingTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void ProcessPendingStorage_WithPendingBlocks_PushToRepoBeforeDownloadingNewBlocks_InMemory()
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

                // Add chained blocks 5 - 9 to PendingStorage
                AddBlockToPendingStorage(fluent.Loop, blocks[5]);
                AddBlockToPendingStorage(fluent.Loop, blocks[6]);
                AddBlockToPendingStorage(fluent.Loop, blocks[7]);
                AddBlockToPendingStorage(fluent.Loop, blocks[8]);
                AddBlockToPendingStorage(fluent.Loop, blocks[9]);

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }
    }
}