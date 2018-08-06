﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreCache
    {
        Task<Block> GetBlockAsync(uint256 blockid);

        /// <summary>
        /// Get the blocks from the database by using block hashes.
        /// </summary>
        /// <param name="blockids">A list of unique Block id hashes.</param>
        /// <returns>The blocks (or null if not found) in the same order as the hashes on input.</returns
        Task<List<Block>> GetBlocksAsync(List<uint256> blockids);

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

        private readonly MemoryCache<uint256, Block> cache;

        public BlockStoreCachePerformanceCounter PerformanceCounter { get; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        public BlockStoreCache(
            IBlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StoreSettings storeSettings)
        {
            Guard.NotNull(blockRepository, nameof(blockRepository));

            this.cache = new MemoryCache<uint256, Block>(storeSettings.MaxCacheBlocksCount);
            this.blockRepository = blockRepository;
            this.dateTimeProvider = dateTimeProvider;
            this.PerformanceCounter = this.BlockStoreCachePerformanceCounterFactory();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public virtual BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new BlockStoreCachePerformanceCounter(this.dateTimeProvider);
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
                this.cache.AddOrUpdate(blockid, block);
                this.PerformanceCounter.AddCacheSetCount(1);
            }

            this.logger.LogTrace("(-)[CACHE_MISS]:'{0}'", block);
            return block;
        }

        /// <inheritdoc />
        public async Task<List<Block>> GetBlocksAsync(List<uint256> blockids)
        {
            Guard.NotNull(blockids, nameof(blockids));

            List<Block> blocks = await this.blockRepository.GetBlocksAsync(blockids);

            var returnBlocks = new List<Block>();

            foreach (Block block in blocks)
            {
                if (this.cache.TryGetValue(block.GetHash(), out Block cachedBlock))
                {
                    this.PerformanceCounter.AddCacheHitCount(1);
                    this.logger.LogTrace("(-)[CACHE_HIT]:'{0}'", block);
                }

                this.PerformanceCounter.AddCacheMissCount(1);

                if (cachedBlock == null)
                {
                    this.cache.AddOrUpdate(block.GetHash(), block);
                    this.PerformanceCounter.AddCacheSetCount(1);

                    returnBlocks.Add(block);
                }
                else
                {
                    returnBlocks.Add(cachedBlock);
                }

                this.logger.LogTrace("(-)[CACHE_MISS]:'{0}'", block);                
            }

            return returnBlocks;
        }


        public void AddToCache(Block block)
        {
            uint256 blockid = block.GetHash();
            this.logger.LogTrace("({0}:'{1}')", nameof(block), blockid);

            this.cache.AddOrUpdate(blockid, block);
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool Exist(uint256 blockid)
        {
            return this.cache.TryGetValue(blockid, out Block unused);
        }
    }
}
