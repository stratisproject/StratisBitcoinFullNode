using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreCacheTest
    {
        private BlockStoreCache blockStoreCache;
        private readonly Mock<IBlockRepository> blockRepository;
        private readonly ILoggerFactory loggerFactory;
        private readonly StoreSettings storeSettings;
        private readonly Network network;

        public BlockStoreCacheTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.network = KnownNetworks.StratisMain;
            this.blockRepository = new Mock<IBlockRepository>();

            this.storeSettings = new StoreSettings();

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.storeSettings);
        }

        [Fact]
        public void GetBlockAsyncBlockInCacheReturnsBlock()
        {
            Block block = this.network.CreateBlock();
            block.Header.Version = 1513;
            this.blockStoreCache.AddToCache(block);

            uint256 hash = block.GetHash();
            Block blockFromCache = this.blockStoreCache.GetBlockAsync(hash).GetAwaiter().GetResult();

            Assert.Equal(1513, blockFromCache.Header.Version);
        }

        [Fact]
        public void GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlock()
        {
            var blockId = new uint256(2389704);
            Block repositoryBlock = this.network.CreateBlock();
            repositoryBlock.Header.Version = 1451;
            this.blockRepository.Setup(b => b.GetAsync(blockId)).Returns(Task.FromResult(repositoryBlock));

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.storeSettings);

            Task<Block> result = this.blockStoreCache.GetBlockAsync(blockId);
            result.Wait();

            Assert.Equal(1451, result.Result.Header.Version);
        }
    }
}