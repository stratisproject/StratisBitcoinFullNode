using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    public sealed class BlockStoreLoopStepCheckNextChainedBlockExistStepTest : BlockStoreLoopStepBaseTest
    {
        [Fact]
        public void CheckNextChainedBlockExists_WithNextChainedBlock_Exists_SetStoreTipAndBlockHash_InMemory()
        {
            var blocks = this.CreateBlocks(5);

            using (var fluent = new FluentBlockStoreLoop(CreateDataFolder(this)))
            {
                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 4 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                this.AppendBlocksToChain(chain, blocks.Skip(1).Take(3));

                // Create the last chained block without appending to the chain
                var block03 = chain.GetBlock(blocks[3].GetHash());
                var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

                fluent.Create(chain);
                Assert.Null(fluent.Loop.StoreTip);

                var nextChainedBlock = block04;
                var checkExistsStep = new CheckNextChainedBlockExistStep(fluent.Loop, this.LoggerFactory.Object);
                checkExistsStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                var options = NetworkOptions.TemporaryOptions;
                Assert.Equal(fluent.Loop.StoreTip.Header.GetHash(options), block04.Header.GetHash(options));
                Assert.Equal(fluent.Loop.BlockRepository.BlockHash, block04.Header.GetHash(options));
            }
        }
    }
}
