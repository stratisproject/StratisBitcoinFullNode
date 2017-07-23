using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepDownloadBlocksTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void CanExecute_DownloadBlocks()
        {
            // Create 15 blocks
            var blocks = CreateBlocks(15);

            // The repository has 5 blocks stored
            var blockRepository = new BlockRepository(Network.TestNet, "test");
            blockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks).GetAwaiter().GetResult();

            // The chain has 10 blocks appended
            var chain = new ConcurrentChain(Network.TestNet);
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

            //Start processing pending blocks from block 5
            var nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[5].GetHash());

            var processPendingStorageStep = new ProcessPendingStorageStep(blockStoreLoop, new CancellationToken());
            processPendingStorageStep.Execute(nextChainedBlock, false).GetAwaiter().GetResult();

            Assert.Equal(blocks[9].GetHash(), blockStoreLoop.BlockRepository.BlockHash);
            Assert.Equal(blocks[9].GetHash(), blockStoreLoop.StoredBlock.HashBlock);
        }
    }
}