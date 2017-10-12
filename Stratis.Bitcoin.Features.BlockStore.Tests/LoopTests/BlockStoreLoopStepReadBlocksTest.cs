using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    public sealed class BlockStoreLoopStepDownloadBlocksTest : BlockStoreLoopStepBaseTest
    {
        /// <summary>
        /// This test executes DownloadBlockStep the <see cref="BlockStoreInnerStepFindBlocks"/> and 
        /// <see cref="BlockStoreInnerStepReadBlocks"/> inner steps via <see cref="DownloadBlockStep"/>
        /// </summary>
        [Fact]
        public async Task BlockStoreInnerStepReadBlocks_WithBlocksToAskAndRead_PushToRepositoryAsync()
        {
            var blocks = CreateBlocks(10);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 5 blocks to the repository
                await fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).ConfigureAwait(false);

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                fluent.Create(chain);

                // Push blocks 5 - 9 to the downloaded blocks collection
                for (int i = 5; i <= 9; i++)
                {
                    fluent.Loop.BlockPuller.InjectBlock(blocks[i].GetHash(), new DownloadedBlock() { Length = blocks[i].GetSerializedSize(), Block = blocks[i] }, new CancellationToken());
                }

                // Start processing blocks to download from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var step = new DownloadBlockStep(fluent.Loop, this.loggerFactory, DateTimeProvider.Default);
                await step.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).ConfigureAwait(false);

                Assert.Equal(blocks[9].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        /// <summary>
        /// Test only BlockStoreInnerStepReadBlocks() inner step
        /// </summary>
        [Fact]
        public async Task BlockStoreInnerStepReadBlocks_WithBlocksToAskAndRead_CanBreakExecutionOnStallCountReachedAsync()
        {
            var blocks = CreateBlocks(3);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 3 blocks to the repository
                await fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks.Take(3).ToList()).ConfigureAwait(false);

                // The chain has 3 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);
                context.DownloadStack.Enqueue(nextChainedBlock);
                context.StallCount = 10001;

                var task = new BlockStoreInnerStepReadBlocks(this.loggerFactory);
                var result = await task.ExecuteAsync(context).ConfigureAwait(false);

                Assert.Equal(InnerStepResult.Stop, result);
            }
        }

        [Fact]
        public async Task BlockStoreInnerStepReadBlocks_CanBreakExecution_DownloadStackIsEmptyAsync()
        {
            var blocks = CreateBlocks(2);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 2 blocks to the repository
                await fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).ConfigureAwait(false);

                // The chain has 2 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(1));

                // Create block store loop
                fluent.Create(chain);

                //Start finding blocks from Block[1]
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[1].GetHash());

                // Create Task Context
                var context = new BlockStoreInnerStepContext(new CancellationToken(), fluent.Loop, nextChainedBlock, this.loggerFactory, DateTimeProvider.Default);
                context.StallCount = 10001;

                var task = new BlockStoreInnerStepReadBlocks(this.loggerFactory);
                var result = await task.ExecuteAsync(context).ConfigureAwait(false);
                Assert.Equal(InnerStepResult.Stop, result);
            }
        }
    }
}