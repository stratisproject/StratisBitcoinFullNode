using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
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
            List<Block> blocks = CreateBlocks(5);

            // The BlockRepository has 5 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\LoopTest_Exists")))
            {
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter();

                var chain = new ConcurrentChain(Network.Main);

                // The chain has 4 blocks appended
                AppendBlocks(chain, blocks.Take(4));

                // Create the last chained block without appending to the chain
                ChainedBlock block03 = chain.GetBlock(blocks[3].GetHash());
                var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

                BlockStoreLoop blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\LoopTest_Exists");

                Assert.Null(blockStoreLoop.StoreTip);

                ChainedBlock nextChainedBlock = block04;
                var checkExistsStep = new CheckNextChainedBlockExistStep(blockStoreLoop);
                checkExistsStep.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blockStoreLoop.StoreTip.Header.GetHash(), block04.Header.GetHash());
                Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block04.Header.GetHash());
            }
        }
    }
}