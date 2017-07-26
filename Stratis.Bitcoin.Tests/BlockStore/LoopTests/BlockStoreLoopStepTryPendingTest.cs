using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
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
            var blocks = CreateBlocks(15);

            // The repository has 5 blocks stored
            var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\LoopTests"));
            blockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

            // The chain has 10 blocks appended
            var chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain, blocks[0]);
            AppendBlock(chain, blocks[1]);
            AppendBlock(chain, blocks[2]);
            AppendBlock(chain, blocks[3]);
            AppendBlock(chain, blocks[4]);
            AppendBlock(chain, blocks[5]);
            AppendBlock(chain, blocks[6]);
            AppendBlock(chain, blocks[7]);
            AppendBlock(chain, blocks[8]);
            AppendBlock(chain, blocks[9]);

            // Create block store loop
            var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository);

            // Add chained blocks 5 - 9 to PendingStorage
            AddToPendingStorage(blockStoreLoop, blocks[5]);
            AddToPendingStorage(blockStoreLoop, blocks[6]);
            AddToPendingStorage(blockStoreLoop, blocks[7]);
            AddToPendingStorage(blockStoreLoop, blocks[8]);
            AddToPendingStorage(blockStoreLoop, blocks[9]);

            //Start processing pending blocks from block 5
            var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[5].GetHash());

            var processPendingStorageStep = new ProcessPendingStorageStep(blockStoreLoop);
            processPendingStorageStep.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

            Assert.Equal(blocks[9].GetHash(), blockStoreLoop.BlockRepository.BlockHash);
            Assert.Equal(blocks[9].GetHash(), blockStoreLoop.StoredBlock.HashBlock);

            blockRepository.Dispose();
            blockRepository = null;
        }
    }
}