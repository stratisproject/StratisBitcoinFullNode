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

        /// <summary>Specifies amount to compact the cache by when the maximum size is exceeded.</summary>
        /// <remarks>For example value of <c>0.8</c> will let cache remove 20% of all items when cache size is exceeded.</remarks>
        private readonly double CompactionPercentage = 0.8;

        public BlockStoreCache(
            IBlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IMemoryCache memoryCache = null)
        {
            Guard.NotNull(blockRepository, nameof(blockRepository));

            // Initialize 'MaxCacheBlocksCount' with default value of maximum 300 blocks or with user defined value.
            // Value of 300 is chosen because it covers most of the cases when not synced node is connected and trying to sync from us.
            this.MaxCacheBlocksCount = nodeSettings.ConfigReader.GetOrDefault("maxCacheBlocksCount", 300);

            this.cache = memoryCache;
            if (this.cache == null)
            {
                var memoryCacheOptions = new MemoryCacheOptions()
                {
                    SizeLimit = this.MaxCacheBlocksCount,
                    CompactionPercentage = this.CompactionPercentage
                };

                this.cache = new MemoryCache(memoryCacheOptions);
            }

            this.blockEntryOptions = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60), Size = 1 };

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
