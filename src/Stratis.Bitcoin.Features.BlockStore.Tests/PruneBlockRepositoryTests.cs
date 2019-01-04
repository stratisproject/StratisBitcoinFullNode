using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore.Pruning;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public sealed class PruneBlockRepositoryTests : LogsTestBase
    {
        public PruneBlockRepositoryTests() : base(new StratisMain())
        {
        }

        [Fact]
        public async Task PruneRepository_PruneAndCompact_FromGenesis_OnStartUpAsync()
        {
            var posBlocks = CreatePosBlocks(50);
            var chainedHeaderTip = BuildProvenHeaderChainFromBlocks(posBlocks);

            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var dBreezeSerializer = new DBreezeSerializer(this.Network);

            var blockRepository = new BlockRepository(this.Network, dataFolder, this.LoggerFactory.Object, dBreezeSerializer);
            await blockRepository.PutAsync(new HashHeightPair(posBlocks.Last().GetHash(), 50), posBlocks);

            var storeSettings = new StoreSettings(NodeSettings.Default(this.Network))
            {
                AmountOfBlocksToKeep = 10
            };

            var prunedBlockRepository = new PrunedBlockRepository(blockRepository, dBreezeSerializer, this.LoggerFactory.Object, storeSettings);
            await prunedBlockRepository.InitializeAsync();
            await prunedBlockRepository.PruneAndCompactDatabase(chainedHeaderTip.GetAncestor(50), this.Network, true);

            // The first prune will delete blocks from 40 to 0.
            Assert.Equal(chainedHeaderTip.GetAncestor(40).HashBlock, prunedBlockRepository.PrunedTip.Hash);
            Assert.Equal(chainedHeaderTip.GetAncestor(40).Height, prunedBlockRepository.PrunedTip.Height);
            // Ensure that the block has been deleted from disk.
            Assert.Null(await blockRepository.GetBlockAsync(chainedHeaderTip.GetAncestor(39).HashBlock));
        }

        [Fact]
        public async Task PruneRepository_PruneAndCompact_MidChain_OnStartUpAsync()
        {
            var posBlocks = CreatePosBlocks(200);
            var chainedHeaderTip = BuildProvenHeaderChainFromBlocks(posBlocks);

            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var dBreezeSerializer = new DBreezeSerializer(this.Network);

            var blockRepository = new BlockRepository(this.Network, dataFolder, this.LoggerFactory.Object, dBreezeSerializer);
            await blockRepository.PutAsync(new HashHeightPair(posBlocks.Take(100).Last().GetHash(), 100), posBlocks.Take(100).ToList());

            var storeSettings = new StoreSettings(NodeSettings.Default(this.Network))
            {
                AmountOfBlocksToKeep = 50
            };

            var prunedBlockRepository = new PrunedBlockRepository(blockRepository, dBreezeSerializer, this.LoggerFactory.Object, storeSettings);
            await prunedBlockRepository.InitializeAsync();

            // The first prune will delete blocks from 50 to 0.
            await prunedBlockRepository.PruneAndCompactDatabase(chainedHeaderTip.GetAncestor(100), this.Network, true);
            Assert.Equal(chainedHeaderTip.GetAncestor(50).HashBlock, prunedBlockRepository.PrunedTip.Hash);
            Assert.Equal(chainedHeaderTip.GetAncestor(50).Height, prunedBlockRepository.PrunedTip.Height);
            // Ensure that the block has been deleted from disk.
            Assert.Null(await blockRepository.GetBlockAsync(chainedHeaderTip.GetAncestor(49).HashBlock));

            // Push more blocks to the repository and prune again.
            // This will delete blocks from height 150 to 50.
            await blockRepository.PutAsync(new HashHeightPair(posBlocks.Skip(100).Take(100).Last().GetHash(), 200), posBlocks.Skip(100).Take(100).ToList());
            await prunedBlockRepository.PruneAndCompactDatabase(chainedHeaderTip, this.Network, true);
            Assert.Equal(chainedHeaderTip.GetAncestor(150).HashBlock, prunedBlockRepository.PrunedTip.Hash);
            Assert.Equal(chainedHeaderTip.GetAncestor(150).Height, prunedBlockRepository.PrunedTip.Height);
            // Ensure that the block has been deleted from disk.
            Assert.Null(await blockRepository.GetBlockAsync(chainedHeaderTip.GetAncestor(149).HashBlock));
        }

        [Fact]
        public async Task PruneRepository_PruneAndCompact_OnShutDownAsync()
        {
            var posBlocks = CreatePosBlocks(50);
            var chainedHeaderTip = BuildProvenHeaderChainFromBlocks(posBlocks);

            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var dBreezeSerializer = new DBreezeSerializer(this.Network);

            var blockRepository = new BlockRepository(this.Network, dataFolder, this.LoggerFactory.Object, dBreezeSerializer);
            await blockRepository.PutAsync(new HashHeightPair(posBlocks.Last().GetHash(), 50), posBlocks);

            var storeSettings = new StoreSettings(NodeSettings.Default(this.Network))
            {
                AmountOfBlocksToKeep = 10
            };

            var prunedBlockRepository = new PrunedBlockRepository(blockRepository, dBreezeSerializer, this.LoggerFactory.Object, storeSettings);
            await prunedBlockRepository.InitializeAsync();

            // Delete blocks 30 to 0 from the repo, this would have been done by the service before shutdown was initiated.
            await blockRepository.DeleteBlocksAsync(posBlocks.Take(30).Select(b => b.GetHash()).ToList());
            prunedBlockRepository.UpdatePrunedTip(chainedHeaderTip.GetAncestor(30));
            // Ensure that the block has been deleted from disk.
            Assert.Null(await blockRepository.GetBlockAsync(chainedHeaderTip.GetAncestor(29).HashBlock));

            // On shutdown the database will only be compacted.
            await prunedBlockRepository.PruneAndCompactDatabase(chainedHeaderTip.GetAncestor(50), this.Network, false);
            Assert.Equal(chainedHeaderTip.GetAncestor(30).HashBlock, prunedBlockRepository.PrunedTip.Hash);
            Assert.Equal(chainedHeaderTip.GetAncestor(30).Height, prunedBlockRepository.PrunedTip.Height);
            Assert.Null(await blockRepository.GetBlockAsync(chainedHeaderTip.GetAncestor(29).HashBlock));
        }
    }
}
