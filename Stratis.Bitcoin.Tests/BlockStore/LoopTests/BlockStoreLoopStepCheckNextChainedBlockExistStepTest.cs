using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepCheckNextChainedBlockExistStepTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void CanExecute_CheckNextChainedBlockExistStep()
        {
            var blocks = CreateBlocks(5);

            // The BlockRepository has 5 blocks stored
            var blockRepository = new BlockRepository(Network.TestNet, "test");
            blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

            // The BlockRepository has 4 blocks appended
            var chain = new ConcurrentChain(Network.TestNet);
            AppendBlock(chain, blocks[0]);
            AppendBlock(chain, blocks[1]);
            AppendBlock(chain, blocks[2]);
            AppendBlock(chain, blocks[3]);

            // Create the last chained block without appending to the chain
            var block03 = chain.GetBlock(blocks[3].GetHash());
            var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

            var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository);

            Assert.Null(blockStoreLoop.StoredBlock);

            var nextChainedBlock = block04;
            var checkExistsStep = new CheckNextChainedBlockExistStep(blockStoreLoop);
            checkExistsStep.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

            Assert.Equal(blockStoreLoop.StoredBlock.Header.GetHash(), block04.Header.GetHash());
            Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block04.Header.GetHash());

            blockRepository.Dispose();
            blockRepository = null;
        }
    }
}