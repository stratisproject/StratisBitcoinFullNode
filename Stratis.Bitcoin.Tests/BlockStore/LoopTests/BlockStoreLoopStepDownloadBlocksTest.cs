using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Tests.BlockStore.LoopTests
{
    public sealed class BlockStoreLoopStepDownloadBlocksTest : BlockStoreLoopStepBaseTest
    {
        /// <summary>
        /// This test executes DownloadBlockStep (DownloadBlocks() and FindBlocks() tasks)
        /// </summary>
        [Fact]
        public void DownloadBlockStep_WithBlocksToFindAndDownload_PushToRepository()
        {
            var blocks = CreateBlocks(10);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                fluent.Create(chain);

                // Push blocks 5 - 9 to the downloaded blocks collection
                fluent.Loop.BlockPuller.InjectBlock(blocks[5].GetHash(), new DownloadedBlock() { Length = blocks[5].GetSerializedSize(), Block = blocks[5] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[6].GetHash(), new DownloadedBlock() { Length = blocks[6].GetSerializedSize(), Block = blocks[6] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[7].GetHash(), new DownloadedBlock() { Length = blocks[7].GetSerializedSize(), Block = blocks[7] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[8].GetHash(), new DownloadedBlock() { Length = blocks[8].GetSerializedSize(), Block = blocks[8] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[9].GetHash(), new DownloadedBlock() { Length = blocks[9].GetSerializedSize(), Block = blocks[9] }, new CancellationToken());

                // Start processing blocks to download from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var step = new DownloadBlockStep(fluent.Loop);
                step.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        [Fact]
        public void DownloadBlockStep_WithBlocksToFindAndDownload_EnsureNextChainedBlockIsAskedForOnStartUp()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                fluent.Create(chain);

                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);
                Assert.Equal(1, context.DownloadStack.Count());
                Assert.True(context.DownloadStack.Any(cb => cb.HashBlock == nextChainedBlock.HashBlock));

                // Push blocks[1] to the downloaded blocks collection
                fluent.Loop.BlockPuller.InjectBlock(blocks[1].GetHash(), new DownloadedBlock() { Length = blocks[1].GetSerializedSize(), Block = blocks[1] }, new CancellationToken());
                Assert.Equal(1, context.BlockStoreLoop.BlockPuller.DownloadedBlocksCount);

                // TryGetBlock should return NextChainedBlock
                DownloadedBlock downloadedBlock = null;
                context.BlockStoreLoop.BlockPuller.TryGetBlock(context.DownloadStack.Peek(), out downloadedBlock);

                Assert.NotNull(downloadedBlock);
                Assert.Equal(downloadedBlock.Block.GetHash(), nextChainedBlock.HashBlock);
            }
        }

        /// <summary>
        /// Test only DownloadBlocks() task
        /// </summary>
        [Fact]
        public void DownloadBlockStep_WithBlocksToFindAndDownload_CanBreakExecutionOnStallCountReached()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 3 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks.Take(3).ToList()).GetAwaiter().GetResult();

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop).Initialize(nextChainedBlock);
                context.StallCount = 10001;

                var task = new BlockStoreInnerStepDownloadBlocks();
                var result = task.ExecuteAsync(context).GetAwaiter().GetResult();

                Assert.Equal(InnerStepResult.Stop, result);
            }
        }
    }
}