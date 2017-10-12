﻿using Microsoft.Extensions.Caching.Memory;
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
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreCacheTest
    {
		private Mock<IIndexRepository> indexRepository;
		private IndexStoreCache indexStoreCache;
		private Mock<IMemoryCache> cache;
        private readonly ILoggerFactory loggerFactory;

		public IndexStoreCacheTest()
		{
            this.loggerFactory = new LoggerFactory();
            this.indexRepository = new Mock<IIndexRepository>();
			this.cache = new Mock<IMemoryCache>();

			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, this.cache.Object, this.loggerFactory);
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
		public async Task GetBlockAsyncBlockInCacheReturnsBlock_IXAsync()
		{
			object block = null;			
			uint256 blockId = new uint256(2389704);
			this.cache.Setup(c => c.TryGetValue(blockId, out block))				
				.Callback(() => {
					block = new Block();
					((Block)block).Header.Version = 1513;
				})
				.Returns(true);

            await this.indexStoreCache.GetBlockAsync(blockId).ConfigureAwait(false);

			Assert.Equal(1513, ((Block)block).Header.Version);
		}

		[Fact]
		public async Task GetBlockAsyncBlockNotInCacheQueriesRepositoryStoresBlockInCacheAndReturnsBlock_IXAsync()
		{
			uint256 blockId = new uint256(2389704);
			Block repositoryBlock = new Block();
			repositoryBlock.Header.Version = 1451;
			this.indexRepository.Setup(b => b.GetAsync(blockId))
				.Returns(Task.FromResult(repositoryBlock));

			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);

			var result = await this.indexStoreCache.GetBlockAsync(blockId).ConfigureAwait(false);

			Assert.Equal(blockId, memoryCacheStub.GetLastCreateCalled());
			Assert.Equal(1451, result.Header.Version);
		}

		[Fact]
		public async Task GetBlockByTrxAsyncBlockInCacheReturnsBlock_IXAsync()
		{
			uint256 txId = new uint256(3252);
			uint256 blockId = new uint256(2389704);
			Block block = new Block();
			block.Header.Version = 1451;
			var dict = new Dictionary<object, object>();
			dict.Add(txId, blockId);
			dict.Add(blockId, block);

			var memoryCacheStub = new MemoryCacheStub(dict);
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);

            var result = await this.indexStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

			Assert.Equal(1451, result.Header.Version);
		}

		[Fact]
		public async Task GetBlockByTrxAsyncBlockNotInCacheLookupInRepository_IXAsync()
		{
			uint256 txId = new uint256(3252);
			uint256 blockId = new uint256(2389704);
			Block block = new Block();
			block.Header.Version = 1451;
			var dict = new Dictionary<object, object>();		
			dict.Add(blockId, block);

			var memoryCacheStub = new MemoryCacheStub(dict);			
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
				.Returns(Task.FromResult(blockId));

			var result = await this.indexStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

			Assert.Equal(1451, result.Header.Version);
			Assert.Equal(txId, memoryCacheStub.GetLastCreateCalled());
		}

		[Fact]
		public async Task GetBlockByTrxAsyncBlockNotInCacheLookupNotInRepositoryReturnsNull_IXAsync()
		{
			uint256 txId = new uint256(3252);			
			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(txId))
				.Returns(Task.FromResult((uint256)null));

            var result = await this.indexStoreCache.GetBlockByTrxAsync(txId).ConfigureAwait(false);

			Assert.Null(result);
		}

		[Fact]
		public async Task GetTrxAsyncReturnsTransactionFromBlockInCache_IXAsync()
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
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);

			var result = await this.indexStoreCache.GetTrxAsync(trans.GetHash()).ConfigureAwait(false);

			Assert.Equal(trans.GetHash(), result.GetHash());
		}

		[Fact]
		public async Task GetTrxAsyncReturnsNullWhenNotInCache_IXAsync()
		{
			var trans = new Transaction();
			trans.Version = 15121;
			this.indexRepository.Setup(b => b.GetTrxBlockIdAsync(trans.GetHash()))
				.Returns(Task.FromResult((uint256)null));

			var memoryCacheStub = new MemoryCacheStub();
			this.indexStoreCache = new IndexStoreCache(this.indexRepository.Object, memoryCacheStub, this.loggerFactory);

			var result = await this.indexStoreCache.GetTrxAsync(trans.GetHash()).ConfigureAwait(false);

			Assert.Null(result);
		}
	}
}
