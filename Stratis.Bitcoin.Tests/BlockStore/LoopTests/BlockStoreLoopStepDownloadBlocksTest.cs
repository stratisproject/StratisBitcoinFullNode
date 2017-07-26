using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
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
            // Create 10 blocks
            List<Block> blocks = CreateBlocks(10);

            // The repository has 5 blocks stored
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.AssureEmptyDirAsDataFolder(@"BlockStore\LoopTest_DownloadBlocks")))
            {
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
                BlockStoreLoop blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository);

                // Push blocks 5 - 9 to the downloaded blocks collection
                blockStoreLoop.BlockPuller.PushBlock(blocks[5].GetSerializedSize(), blocks[5], new CancellationToken());
                blockStoreLoop.BlockPuller.PushBlock(blocks[6].GetSerializedSize(), blocks[6], new CancellationToken());
                blockStoreLoop.BlockPuller.PushBlock(blocks[7].GetSerializedSize(), blocks[7], new CancellationToken());
                blockStoreLoop.BlockPuller.PushBlock(blocks[8].GetSerializedSize(), blocks[8], new CancellationToken());
                blockStoreLoop.BlockPuller.PushBlock(blocks[9].GetSerializedSize(), blocks[9], new CancellationToken());

                //Start processing blocks to download from block 5
                ChainedBlock nextChainedBlock = blockStoreLoop.Chain.GetBlock(blocks[5].GetHash());

                var step = new DownloadBlockStep(blockStoreLoop);
                step.Execute(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), blockStoreLoop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), blockStoreLoop.StoredBlock.HashBlock);

                chain = null;
            }
        }
    }
}