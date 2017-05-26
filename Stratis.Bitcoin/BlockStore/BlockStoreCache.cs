using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockStore
{
	public interface IBlockStoreCache : IDisposable
	{
		void Expire(uint256 blockid);
		Task<Block> GetBlockAsync(uint256 blockid);
		Task<Block> GetBlockByTrxAsync(uint256 trxid);
		Task<Transaction> GetTrxAsync(uint256 trxid);

	}
	public class BlockStoreCache : IBlockStoreCache
	{
		private readonly IBlockRepository blockRepository;
		private readonly IMemoryCache cache;
		public BlockStoreCachePerformanceCounter PerformanceCounter { get; }	

		public BlockStoreCache(BlockRepository blockRepository) : this(blockRepository, new MemoryCache(new MemoryCacheOptions()))
		{
		}

		public BlockStoreCache(IBlockRepository blockRepository, IMemoryCache memoryCache)
		{
			Guard.NotNull(blockRepository, nameof(blockRepository));
			Guard.NotNull(memoryCache, nameof(memoryCache));

			this.blockRepository = blockRepository;
			this.cache = memoryCache;
			this.PerformanceCounter = new BlockStoreCachePerformanceCounter();
		}

		public void Expire(uint256 blockid)
		{
			Guard.NotNull(blockid, nameof(blockid));

			Block block;
			if (this.cache.TryGetValue(blockid, out block))
			{
				this.PerformanceCounter.AddCacheHitCount(1);
                this.PerformanceCounter.AddCacheRemoveCount(1);
                this.cache.Remove(block);                
			}
		}

		public async Task<Block> GetBlockAsync(uint256 blockid)
		{
			Guard.NotNull(blockid, nameof(blockid));

			Block block;
			if (this.cache.TryGetValue(blockid, out block))
			{
                this.PerformanceCounter.AddCacheHitCount(1);
				return block;
			}

            this.PerformanceCounter.AddCacheMissCount(1);

			block = await this.blockRepository.GetAsync(blockid);
			if (block != null)
			{
				this.cache.Set(blockid, block, TimeSpan.FromMinutes(10));
				this.PerformanceCounter.AddCacheSetCount(1);
			}

			return block;
		}

		public async Task<Block> GetBlockByTrxAsync(uint256 trxid)
		{
			Guard.NotNull(trxid, nameof(trxid));

			uint256 blokcid;
			Block block;
			if (this.cache.TryGetValue(trxid, out blokcid))
			{
                this.PerformanceCounter.AddCacheHitCount(1);
				block = await this.GetBlockAsync(blokcid);
				return block;
			}

            this.PerformanceCounter.AddCacheMissCount(1);

			blokcid = await this.blockRepository.GetTrxBlockIdAsync(trxid);
			if (blokcid == null)
				return null;

			this.cache.Set(trxid, blokcid, TimeSpan.FromMinutes(10));
            this.PerformanceCounter.AddCacheSetCount(1);
			block = await this.GetBlockAsync(blokcid);

			return block;
		}

		public async Task<Transaction> GetTrxAsync(uint256 trxid)
		{
			Guard.NotNull(trxid, nameof(trxid));

			var block = await this.GetBlockByTrxAsync(trxid);
			return block?.Transactions.Find(t => t.GetHash() == trxid);
		}

		public void Dispose()
		{
			this.cache.Dispose();
		}
	}
}
