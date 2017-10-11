namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NBitcoin;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;
    using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;

    public class BlockStoreCacheTest
    {
        private Mock<IBlockRepository> blockRepository;
        private BlockStoreCache blockStoreCache;
        private Mock<IMemoryCache> cache;
        private readonly ILoggerFactory loggerFactory;

        public BlockStoreCacheTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.blockRepository = new Mock<IBlockRepository>();
            this.cache = new Mock<IMemoryCache>();

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, this.cache.Object, this.loggerFactory);
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
        public async Task GetBlockAsyncBlockInCacheReturnsBlockAsync()
        {
            object block = null;
            uint256 blockId = new uint256(2389704);
            this.cache.Setup(c => c.TryGetValue(blockId, out block))
                .Callback(() => {
                    block = new Block();
                    ((Block)block).Header.Version = 1513;
                })
                .Returns(true);

            await this.blockStoreCache.GetBlockAsync(blockId).ConfigureAwait(false);

            Assert.Equal(1513, ((Block)block).Header.Version);
        }

        [Fact]
        public async Task GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlockAsync()
        {
            uint256 blockId = new uint256(2389704);
            Block repositoryBlock = new Block();
            repositoryBlock.Header.Version = 1451;
            this.blockRepository.Setup(b => b.GetAsync(blockId))
                .Returns(Task.FromResult(repositoryBlock));

            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);

            var result = await this.blockStoreCache.GetBlockAsync(blockId).ConfigureAwait(false);

            Assert.Equal(blockId, memoryCacheStub.GetLastCreateCalled());
            Assert.Equal(1451, result.Header.Version);
        }

        [Fact]
        public async Task GetBlockByTrxAsyncBlockInCacheReturnsBlockAsync()
        {
            uint256 txId = new uint256(3252);
            uint256 blockId = new uint256(2389704);
            Block block = new Block();
            block.Header.Version = 1451;
            var dict = new Dictionary<object, object>();
            dict.Add(txId, blockId);
            dict.Add(blockId, block);

            var memoryCacheStub = new MemoryCacheStub(dict);
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);

            var result = await this.blockStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

            Assert.Equal(1451, result.Header.Version);
        }

        [Fact]
        public async Task GetBlockByTrxAsyncBlockNotInCacheLookupInRepositoryAsync()
        {
            uint256 txId = new uint256(3252);
            uint256 blockId = new uint256(2389704);
            Block block = new Block();
            block.Header.Version = 1451;
            var dict = new Dictionary<object, object>();
            dict.Add(blockId, block);

            var memoryCacheStub = new MemoryCacheStub(dict);
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
                .Returns(Task.FromResult(blockId));

            var result = await this.blockStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

            Assert.Equal(1451, result.Header.Version);
            Assert.Equal(txId, memoryCacheStub.GetLastCreateCalled());
        }

        [Fact]
        public async Task GetBlockByTrxAsyncBlockNotInCacheLookupNotInRepositoryReturnsNullAsync()
        {
            uint256 txId = new uint256(3252);
            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
                .Returns(Task.FromResult((uint256)null));

            var result = await this.blockStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTrxAsyncReturnsTransactionFromBlockInCacheAsync()
        {
            var trans = new Transaction();
            trans.Version = 15121;
            uint256 blockId = new uint256(2389704);
            Block block = new Block();
            block.Header.Version = 1451;
            block.Transactions.Add(trans);

            var dict = new Dictionary<object, object>();
            dict.Add(trans.GetHash(), blockId);
            dict.Add(blockId, block);

            var memoryCacheStub = new MemoryCacheStub(dict);
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);

            var result = await this.blockStoreCache.GetTrxAsync(trans.GetHash()).ConfigureAwait(false);

            Assert.Equal(trans.GetHash(), result.GetHash());
        }

        [Fact]
        public async Task GetTrxAsyncReturnsNullWhenNotInCacheAsync()
        {
            var trans = new Transaction();
            trans.Version = 15121;
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(trans.GetHash()))
                .Returns(Task.FromResult((uint256)null));

            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, this.loggerFactory);

            var result = await this.blockStoreCache.GetTrxAsync(trans.GetHash()).ConfigureAwait(false);

            Assert.Null(result);
        }
    }
}
