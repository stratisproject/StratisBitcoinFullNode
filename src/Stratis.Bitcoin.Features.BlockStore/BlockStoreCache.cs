using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreCache : IDisposable
    {
        void Expire(uint256 blockid);

        Task<Block> GetBlockAsync(uint256 blockid);

        Task<Block> GetBlockByTrxAsync(uint256 trxid);

        Task<Transaction> GetTrxAsync(uint256 trxid);

        void AddToCache(Block block);

        /// <summary>
        /// Determine if a block already exists in the cache.
        /// </summary>
        /// <param name="blockid">Block id.</param>
        /// <returns><c>true</c> if the block hash can be found in the cache, otherwise return <c>false</c>.</returns>
        bool Exist(uint256 blockid);
    }

    public class BlockStoreCache : IBlockStoreCache
    {
        private readonly IBlockRepository blockRepository;

        private readonly IMemoryCache cache;

        public BlockStoreCachePerformanceCounter PerformanceCounter { get; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The maximum amount of blocks the cache can contain.</summary>
        public readonly int MaxCacheBlocksCount;

        /// <summary>Entry options for adding blocks to the cache.</summary>
        private readonly MemoryCacheEntryOptions blockEntryOptions;

        /// <summary>Entry options for adding transactions to the cache.</summary>
        private readonly MemoryCacheEntryOptions txEntryOptions;

        /// <summary>Specifies amount to compact the cache by when the maximum size is exceeded.</summary>
        /// <remarks>For example value of 0.8 will let cache remove 20% of all items when cache size is exceeded.</remarks>
        private readonly double CompactionPercentage = 0.8;

        public BlockStoreCache(
            IBlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IMemoryCache memoryCache = null)
        {
            Guard.NotNull(blockRepository, nameof(blockRepository));

            this.MaxCacheBlocksCount = nodeSettings.ConfigReader.GetOrDefault("maxCacheBlocksCount", 300);

            this.cache = memoryCache;
            if (this.cache == null)
            {
                var memoryCacheOptions = new MemoryCacheOptions()
                {
                    //We treat one block to be of '100' size and tx to be '1'.
                    SizeLimit = this.MaxCacheBlocksCount * 100,

                    CompactionPercentage = this.CompactionPercentage
                };

                this.cache = new MemoryCache(memoryCacheOptions);
            }

            this.blockEntryOptions = new MemoryCacheEntryOptions() {AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60), Size = 100 };
            this.txEntryOptions = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60), Size = 1 };

            this.blockRepository = blockRepository;
            this.dateTimeProvider = dateTimeProvider;
            this.PerformanceCounter = this.BlockStoreCachePerformanceCounterFactory();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public virtual BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new BlockStoreCachePerformanceCounter(this.dateTimeProvider);
        }

        public void Expire(uint256 blockid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockid), blockid);
            Guard.NotNull(blockid, nameof(blockid));

            Block block;
            if (this.cache.TryGetValue(blockid, out block))
            {
                this.PerformanceCounter.AddCacheHitCount(1);
                this.PerformanceCounter.AddCacheRemoveCount(1);
                this.cache.Remove(block);
            }

            this.logger.LogTrace("(-)");
        }

        public async Task<Block> GetBlockAsync(uint256 blockid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockid), blockid);
            Guard.NotNull(blockid, nameof(blockid));

            Block block;
            if (this.cache.TryGetValue(blockid, out block))
            {
                this.PerformanceCounter.AddCacheHitCount(1);
                this.logger.LogTrace("(-)[CACHE_HIT]:'{0}'", block);
                return block;
            }

            this.PerformanceCounter.AddCacheMissCount(1);

            block = await this.blockRepository.GetAsync(blockid);
            if (block != null)
            {
                this.cache.Set(blockid, block, this.blockEntryOptions);
                this.PerformanceCounter.AddCacheSetCount(1);
            }

            this.logger.LogTrace("(-)[CACHE_MISS]:'{0}'", block);
            return block;
        }

        public async Task<Block> GetBlockByTrxAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);
            Guard.NotNull(trxid, nameof(trxid));

            uint256 blockid;
            Block block;
            if (this.cache.TryGetValue(trxid, out blockid))
            {
                this.PerformanceCounter.AddCacheHitCount(1);
                block = await this.GetBlockAsync(blockid);
                return block;
            }

            this.PerformanceCounter.AddCacheMissCount(1);

            blockid = await this.blockRepository.GetTrxBlockIdAsync(trxid);
            if (blockid == null)
            {
                this.logger.LogTrace("(-):null");
                return null;
            }

            this.cache.Set(trxid, blockid, this.txEntryOptions);
            this.PerformanceCounter.AddCacheSetCount(1);
            block = await this.GetBlockAsync(blockid);

            this.logger.LogTrace("(-):'{0}'", block);
            return block;
        }

        public async Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);
            Guard.NotNull(trxid, nameof(trxid));

            Block block = await this.GetBlockByTrxAsync(trxid);
            Transaction trx = block?.Transactions.Find(t => t.GetHash() == trxid);

            this.logger.LogTrace("(-):'{0}')", trx);
            return trx;
        }

        public void AddToCache(Block block)
        {
            uint256 blockid = block.GetHash();
            this.logger.LogTrace("({0}:'{1}')", nameof(block), blockid);

            if (!this.cache.TryGetValue(blockid, out Block existingBlock))
                this.cache.Set(blockid, block, this.blockEntryOptions);

            this.logger.LogTrace("(-)[{0}]", existingBlock != null ? "ALREADY_IN_CACHE" : "ADDED_TO_CACHE");
        }

        /// <inheritdoc />
        public bool Exist(uint256 blockid)
        {
            return this.cache.TryGetValue(blockid, out Block unused);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cache.Dispose();
        }
    }
}
