using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Primitives;
using Stratis.Bitcoin.Features.IndexStore;
using IIndexRepository = Stratis.Bitcoin.Features.IndexStore.IIndexRepository;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreCacheTest
    {
		private Mock<IIndexRepository> indexRepository;
		private IndexStoreCache indexStoreCache;
		private Mock<IMemoryCache> cache;

		public IndexStoreCacheTest()
		{
			this.indexRepository = new Mock<IIndexRepository>();
			this.cache = new Mock<IMemoryCache>();

			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, this.cache.Object);
		}

		[Fact]
		public void ExpireRemovesBlockFromCacheWhenExists_IX()
		{
			object block = null;
			uint256 blockId = new uint256(2389704);
			this.cache.Setup(c => c.TryGetValue(blockId, out block))
				.Returns(true);

			this.indexStoreCache.Expire(blockId);

			this.cache.Verify(c => c.Remove(It.IsAny<Block>()), Times.Exactly(1));
		}

		[Fact]
		public void ExpireDoesNotRemoveBlockFromCacheWhenNotExists_IX()
		{
			object block = null;
			uint256 blockId = new uint256(2389704);
			this.cache.Setup(c => c.TryGetValue(blockId, out block))
				.Returns(false);

			this.indexStoreCache.Expire(blockId);

			this.cache.Verify(c => c.Remove(It.IsAny<Block>()), Times.Exactly(0));
		}

		[Fact]
		public void GetBlockAsyncBlockInCacheReturnsBlock_IX()
		{
			object block = null;			
			uint256 blockId = new uint256(2389704);
			this.cache.Setup(c => c.TryGetValue(blockId, out block))				
				.Callback(() => {
					block = new Block();
					((Block)block).Header.Version = 1513;
				})
				.Returns(true);

			var task = this.indexStoreCache.GetBlockAsync(blockId);
			task.Wait();

			Assert.Equal(1513, ((Block)block).Header.Version);
		}

		[Fact]
		public void GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlock_IX()
		{
			uint256 blockId = new uint256(2389704);
			Block repositoryBlock = new Block();
			repositoryBlock.Header.Version = 1451;
			this.indexRepository.Setup(b => b.GetAsync(blockId))
				.Returns(Task.FromResult(repositoryBlock));

			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);

			var result = this.indexStoreCache.GetBlockAsync(blockId);
			result.Wait();

			Assert.Equal(blockId, memoryCacheStub.GetLastCreateCalled());
			Assert.Equal(1451, result.Result.Header.Version);
		}

		[Fact]
		public void GetBlockByTrxAsyncBlockInCacheReturnsBlock_IX()
		{
			uint256 txId = new uint256(3252);
			uint256 blockId = new uint256(2389704);
			Block block = new Block();
			block.Header.Version = 1451;
			var dict = new Dictionary<object, object>();
			dict.Add(txId, blockId);
			dict.Add(blockId, block);

			var memoryCacheStub = new MemoryCacheStub(dict);
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);

			var result = this.indexStoreCache.GetBlockByTrxAsync(txId);
			result.Wait();

			Assert.Equal(1451, result.Result.Header.Version);
		}

		[Fact]
		public void GetBlockByTrxAsyncBlockNotInCacheLookupInRepository_IX()
		{
			uint256 txId = new uint256(3252);
			uint256 blockId = new uint256(2389704);
			Block block = new Block();
			block.Header.Version = 1451;
			var dict = new Dictionary<object, object>();		
			dict.Add(blockId, block);

			var memoryCacheStub = new MemoryCacheStub(dict);			
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
				.Returns(Task.FromResult(blockId));

			var result = this.indexStoreCache.GetBlockByTrxAsync(txId);
			result.Wait();

			Assert.Equal(1451, result.Result.Header.Version);
			Assert.Equal(txId, memoryCacheStub.GetLastCreateCalled());
		}

		[Fact]
		public void GetBlockByTrxAsyncBlockNotInCacheLookupNotInRepositoryReturnsNull_IX()
		{
			uint256 txId = new uint256(3252);			
			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
				.Returns(Task.FromResult((uint256)null));

			var result = this.indexStoreCache.GetBlockByTrxAsync(txId);
			result.Wait();

			Assert.Null(result.Result);
		}

		[Fact]
		public void GetTrxAsyncReturnsTransactionFromBlockInCache_IX()
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
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);

			var result = this.indexStoreCache.GetTrxAsync(trans.GetHash());
			result.Wait();

			Assert.Equal(trans.GetHash(), result.Result.GetHash());
		}

		[Fact]
		public void GetTrxAsyncReturnsNullWhenNotInCache_IX()
		{
			var trans = new Transaction();
			trans.Version = 15121;
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(trans.GetHash()))
				.Returns(Task.FromResult((uint256)null));

			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub);

			var result = this.indexStoreCache.GetTrxAsync(trans.GetHash());
			result.Wait();

			Assert.Null(result.Result);
		}
	}
}
