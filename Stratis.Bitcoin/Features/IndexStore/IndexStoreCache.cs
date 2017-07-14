using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexStoreCache : IDisposable
    {
        void Expire(uint256 blockid);
        Task<Block> GetBlockAsync(uint256 blockid);
        Task<Block> GetBlockByTrxAsync(uint256 trxid);
        Task<Transaction> GetTrxAsync(uint256 trxid);
        void AddToCache(Block block);
    }
    public class IndexStoreCache : IIndexStoreCache
    {
        private readonly IIndexRepository indexRepository;
        private readonly IMemoryCache cache;
        public IndexStoreCachePerformanceCounter PerformanceCounter { get; }

        public IndexStoreCache(IndexRepository indexRepository) : this(indexRepository, new MemoryCache(new MemoryCacheOptions()))
        {
        }

        public IndexStoreCache(IIndexRepository indexRepository, IMemoryCache memoryCache)
        {
            Guard.NotNull(indexRepository, nameof(indexRepository));
            Guard.NotNull(memoryCache, nameof(memoryCache));

            this.indexRepository = indexRepository;
            this.cache = memoryCache;
            this.PerformanceCounter = new IndexStoreCachePerformanceCounter();
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

            block = await this.indexRepository.GetAsync(blockid);
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

            blokcid = await this.indexRepository.GetTrxBlockIdAsync(trxid);
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

        public void AddToCache(Block block)
        {
            var blockid = block.GetHash();
            Block unused;
            if (!this.cache.TryGetValue(blockid, out unused))
                this.cache.Set(blockid, block, TimeSpan.FromMinutes(10));
        }

        public void Dispose()
        {
            this.cache.Dispose();
        }
    }
}
