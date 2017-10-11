using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    public sealed class BlockStoreLoopStepCheckNextChainedBlockExistStepTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public async Task CheckNextChainedBlockExists_WithNextChainedBlock_Exists_SetStoreTipAndBlockHash_InMemoryAsync()
        {
            var blocks = CreateBlocks(5);

            using (var fluent = new FluentBlockStoreLoop())
            {
                // Push 5 blocks to the repository
                await fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).ConfigureAwait(false);

                // The chain has 4 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(3));

                // Create the last chained block without appending to the chain
                var block03 = chain.GetBlock(blocks[3].GetHash());
                var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

                fluent.Create(chain);
                Assert.Null(fluent.Loop.StoreTip);

                var nextChainedBlock = block04;
                var checkExistsStep = new CheckNextChainedBlockExistStep(fluent.Loop, this.loggerFactory);
                await checkExistsStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).ConfigureAwait(false);

                Assert.Equal(fluent.Loop.StoreTip.Header.GetHash(), block04.Header.GetHash());
                Assert.Equal(fluent.Loop.BlockRepository.BlockHash, block04.Header.GetHash());
            }
        }
    }
}