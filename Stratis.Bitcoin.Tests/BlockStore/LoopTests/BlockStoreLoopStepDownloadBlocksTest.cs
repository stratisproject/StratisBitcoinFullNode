using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

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

                var chain = new ConcurrentChain(Network.Main);

                // The chain has 10 blocks appended
                AppendBlocks(chain, blocks.Take(10));

                // Create block store loop
                BlockStoreLoop blockStoreLoop = CreateBlockStoreLoop(chain, blockRepository, @"BlockStore\LoopTest_DownloadBlocks");

                // Push blocks 5 - 9 to the downloaded blocks collection
                blockStoreLoop.BlockPuller.InjectBlock(blocks[5].GetHash(), new DownloadedBlock() { Length = blocks[5].GetSerializedSize(), Block = blocks[5] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[6].GetHash(), new DownloadedBlock() { Length = blocks[6].GetSerializedSize(), Block = blocks[6] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[7].GetHash(), new DownloadedBlock() { Length = blocks[7].GetSerializedSize(), Block = blocks[7] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[8].GetHash(), new DownloadedBlock() { Length = blocks[8].GetSerializedSize(), Block = blocks[8] }, new CancellationToken());
                blockStoreLoop.BlockPuller.InjectBlock(blocks[9].GetHash(), new DownloadedBlock() { Length = blocks[9].GetSerializedSize(), Block = blocks[9] }, new CancellationToken());

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