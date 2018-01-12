using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreCacheTest
    {
        private Mock<IBlockRepository> blockRepository;
        private BlockStoreCache blockStoreCache;
        private Mock<IMemoryCache> cache;
        private readonly ILoggerFactory loggerFactory;
        private readonly NodeSettings nodeSettings;

        public BlockStoreCacheTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.blockRepository = new Mock<IBlockRepository>();
            this.cache = new Mock<IMemoryCache>();

            this.nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            this.nodeSettings.LoadArguments(new string[] { });

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings, this.cache.Object);
        }

        [Fact]
        public void ExpireRemovesBlockFromCacheWhenExists()
        {
            object block = null;
            uint256 blockId = new uint256(2389704);
            this.cache.Setup(c => c.TryGetValue(blockId, out block))
                .Returns(true);

            this.blockStoreCache.Expire(blockId);

            this.cache.Verify(c => c.Remove(It.IsAny<Block>()), Times.Exactly(1));
        }

        [Fact]
        public void ExpireDoesNotRemoveBlockFromCacheWhenNotExists()
        {
            object block = null;
            uint256 blockId = new uint256(2389704);
            this.cache.Setup(c => c.TryGetValue(blockId, out block))
                .Returns(false);

            this.blockStoreCache.Expire(blockId);

            this.cache.Verify(c => c.Remove(It.IsAny<Block>()), Times.Exactly(0));
        }

        [Fact]
        public void GetBlockAsyncBlockInCacheReturnsBlock()
        {
            object block = null;
            uint256 blockId = new uint256(2389704);
            this.cache.Setup(c => c.TryGetValue(blockId, out block))
                .Callback(() =>
                {
                    block = new Block();
                    ((Block)block).Header.Version = 1513;
                })
                .Returns(true);

            var task = this.blockStoreCache.GetBlockAsync(blockId);
            task.Wait();

            Assert.Equal(1513, ((Block)block).Header.Version);
        }

        [Fact]
        public void GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlock()
        {
            uint256 blockId = new uint256(2389704);
            Block repositoryBlock = new Block();
            repositoryBlock.Header.Version = 1451;
            this.blockRepository.Setup(b => b.GetAsync(blockId))
                .Returns(Task.FromResult(repositoryBlock));

            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, DateTimeProvider.Default, this.loggerFactory, this.nodeSettings, memoryCacheStub);

            var result = this.blockStoreCache.GetBlockAsync(blockId);
            result.Wait();

            Assert.Equal(blockId, memoryCacheStub.GetLastCreateCalled());
            Assert.Equal(1451, result.Result.Header.Version);
        }
    }
}
