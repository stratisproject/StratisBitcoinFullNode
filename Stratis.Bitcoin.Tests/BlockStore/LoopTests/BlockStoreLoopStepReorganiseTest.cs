using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepReorganiseTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void CanExecute_Reorganise()
        {
            var blocks = CreateBlocks(15);

            // The BlockRepository has 15 blocks stored
            var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\LoopTests"));
            blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

            // The BlockRepository has 10 blocks appended
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

            // Create the last 5 chained blocks without appending to the chain
            var block9 = chain.GetBlock(blocks[9].GetHash());
            var block10 = new ChainedBlock(blocks[10].Header, blocks[10].Header.GetHash(), block9);
            var block11 = new ChainedBlock(blocks[11].Header, blocks[11].Header.GetHash(), block10);
            var block12 = new ChainedBlock(blocks[12].Header, blocks[12].Header.GetHash(), block11);
            var block13 = new ChainedBlock(blocks[13].Header, blocks[13].Header.GetHash(), block12);
            var block14 = new ChainedBlock(blocks[14].Header, blocks[14].Header.GetHash(), block13);

            var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository);
            blockStoreLoop.StoredBlock = block14;

            Assert.Equal(blockStoreLoop.StoredBlock.Header.GetHash(), block14.Header.GetHash());
            Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block14.Header.GetHash());

            //Reorganise (delete) blocks from the block repository that is not found
            var nextChainedBlock = block10;
            var reorganiseStep = new ReorganiseBlockRepositoryStep(blockStoreLoop);
            reorganiseStep.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

            Assert.Equal(blockStoreLoop.StoredBlock.Header.GetHash(), block10.Previous.Header.GetHash());
            Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block10.Previous.Header.GetHash());

            blockRepository.Dispose();
            blockRepository = null;
        }
    }
}