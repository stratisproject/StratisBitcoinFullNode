using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
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

            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\CanExecute_CheckNextChainedBlockExistStep")))
            {
                // Push 5 blocks to the repository
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 4 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(3));

                // Create the last chained block without appending to the chain
                var block03 = chain.GetBlock(blocks[3].GetHash());
                var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\CanExecute_CheckNextChainedBlockExistStep");
                Assert.Null(blockStoreLoop.StoreTip);

                var nextChainedBlock = block04;
                var checkExistsStep = new CheckNextChainedBlockExistStep(blockStoreLoop);
                checkExistsStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blockStoreLoop.StoreTip.Header.GetHash(), block04.Header.GetHash());
                Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block04.Header.GetHash());
            }
        }
    }
}