using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
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
            List<Block> blocks = CreateBlocks(15);

            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\CanExecute_Reorganise")))
            {
                // Push 15 blocks to the repository
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocks(chain, blocks.Skip(1).Take(9));

                // Create the last 5 chained blocks without appending to the chain
                var block9 = chain.GetBlock(blocks[9].Header.GetHash());
                var block10 = new ChainedBlock(blocks[10].Header, blocks[10].Header.GetHash(), block9);
                var block11 = new ChainedBlock(blocks[11].Header, blocks[11].Header.GetHash(), block10);
                var block12 = new ChainedBlock(blocks[12].Header, blocks[12].Header.GetHash(), block11);
                var block13 = new ChainedBlock(blocks[13].Header, blocks[13].Header.GetHash(), block12);
                var block14 = new ChainedBlock(blocks[14].Header, blocks[14].Header.GetHash(), block13);

                var blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\CanExecute_Reorganise");
                blockStoreLoop.SetStoreTip(block14);

                Assert.Equal(blockStoreLoop.StoreTip.Header.GetHash(), block14.Header.GetHash());
                Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block14.Header.GetHash());

                //Reorganise (delete) blocks from the block repository that is not found
                var nextChainedBlock = block10;
                var reorganiseStep = new ReorganiseBlockRepositoryStep(blockStoreLoop);
                reorganiseStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blockStoreLoop.StoreTip.Header.GetHash(), block10.Previous.Header.GetHash());
                Assert.Equal(blockStoreLoop.BlockRepository.BlockHash, block10.Previous.Header.GetHash());
            }
        }
    }
}