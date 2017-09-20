﻿namespace Stratis.Bitcoin.Features.BlockStore
{
    using System;
    using System.Text;
    using Microsoft.Extensions.Logging;

    public sealed class BlockStoreStats
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private IBlockRepository repository;
        private BlockStoreCache cache;
        private BlockStoreRepositoryPerformanceSnapshot lastRepositorySnapshot;
        private BlockStoreCachePerformanceSnapshot lastCacheSnapshot;

        public BlockStoreStats(IBlockRepository blockRepository, IBlockStoreCache blockStoreCache, ILogger logger)
        {
            this.repository = blockRepository;
            this.cache = blockStoreCache as BlockStoreCache;
            this.logger = logger;
            this.lastRepositorySnapshot = this.repository?.PerformanceCounter.Snapshot();
            this.lastCacheSnapshot = this.cache?.PerformanceCounter.Snapshot();
        }

        public bool CanLog
        {
            get
            {
                return (DateTimeOffset.UtcNow - this.lastRepositorySnapshot.Taken) > TimeSpan.FromSeconds(10.0);
            }
        }

        public void Log()
        {
            StringBuilder performanceLogBuilder = new StringBuilder();

            if (this.repository != null)
            {
                var snapshot = this.repository.PerformanceCounter.Snapshot();
                performanceLogBuilder.AppendLine((snapshot - this.lastRepositorySnapshot).ToString());
                this.lastRepositorySnapshot = snapshot;
            }

            if (this.cache != null)
            {
                var snapshot = this.cache.PerformanceCounter.Snapshot();
                performanceLogBuilder.AppendLine((snapshot - this.lastCacheSnapshot).ToString());
                this.lastCacheSnapshot = snapshot;
            }

            this.logger.LogInformation(performanceLogBuilder.ToString());
        }
    }
}