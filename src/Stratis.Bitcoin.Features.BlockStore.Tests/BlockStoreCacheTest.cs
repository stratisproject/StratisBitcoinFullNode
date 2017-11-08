using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
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

            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, this.cache.Object, DateTimeProvider.Default, this.loggerFactory);
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
                .Callback(() => {
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
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);

            var result = this.blockStoreCache.GetBlockAsync(blockId);
            result.Wait();

            Assert.Equal(blockId, memoryCacheStub.GetLastCreateCalled());
            Assert.Equal(1451, result.Result.Header.Version);
        }

        [Fact]
        public void GetBlockByTrxAsyncBlockInCacheReturnsBlock()
        {
            uint256 txId = new uint256(3252);
            uint256 blockId = new uint256(2389704);
            Block block = new Block();
            block.Header.Version = 1451;
            var dict = new Dictionary<object, object>();
            dict.Add(txId, blockId);
            dict.Add(blockId, block);

            var memoryCacheStub = new MemoryCacheStub(dict);
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);

            var result = this.blockStoreCache.GetBlockByTrxAsync(txId);
            result.Wait();

            Assert.Equal(1451, result.Result.Header.Version);
        }

        [Fact]
        public void GetBlockByTrxAsyncBlockNotInCacheLookupInRepository()
        {
            uint256 txId = new uint256(3252);
            uint256 blockId = new uint256(2389704);
            Block block = new Block();
            block.Header.Version = 1451;
            var dict = new Dictionary<object, object>();
            dict.Add(blockId, block);

            var memoryCacheStub = new MemoryCacheStub(dict);
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
                .Returns(Task.FromResult(blockId));

            var result = this.blockStoreCache.GetBlockByTrxAsync(txId);
            result.Wait();

            Assert.Equal(1451, result.Result.Header.Version);
            Assert.Equal(txId, memoryCacheStub.GetLastCreateCalled());
        }

        [Fact]
        public void GetBlockByTrxAsyncBlockNotInCacheLookupNotInRepositoryReturnsNull()
        {
            uint256 txId = new uint256(3252);
            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
                .Returns(Task.FromResult((uint256)null));

            var result = this.blockStoreCache.GetBlockByTrxAsync(txId);
            result.Wait();

            Assert.Null(result.Result);
        }

        [Fact]
        public void GetTrxAsyncReturnsTransactionFromBlockInCache()
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
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);

            var result = this.blockStoreCache.GetTrxAsync(trans.GetHash());
            result.Wait();

            Assert.Equal(trans.GetHash(), result.Result.GetHash());
        }

        [Fact]
        public void GetTrxAsyncReturnsNullWhenNotInCache()
        {
            var trans = new Transaction();
            trans.Version = 15121;
            this.blockRepository.Setup(b => b.GetTrxBlockIdAsync(trans.GetHash()))
                .Returns(Task.FromResult((uint256)null));

            var memoryCacheStub = new MemoryCacheStub();
            this.blockStoreCache = new BlockStoreCache(this.blockRepository.Object, memoryCacheStub, DateTimeProvider.Default, this.loggerFactory);

            var result = this.blockStoreCache.GetTrxAsync(trans.GetHash());
            result.Wait();

            Assert.Null(result.Result);
        }
    }
}
