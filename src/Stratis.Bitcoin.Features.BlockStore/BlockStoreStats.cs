using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public sealed class BlockStoreStats
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private IBlockRepository repository;
        private BlockStoreCache cache;
        private BlockStoreRepositoryPerformanceSnapshot lastRepositorySnapshot;
        private BlockStoreCachePerformanceSnapshot lastCacheSnapshot;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public BlockStoreStats(IBlockRepository blockRepository, IBlockStoreCache blockStoreCache, IDateTimeProvider dateTimeProvider, ILogger logger)
        {
            this.repository = blockRepository;
            this.cache = blockStoreCache as BlockStoreCache;
            this.logger = logger;
            this.lastRepositorySnapshot = this.repository?.PerformanceCounter.Snapshot();
            this.lastCacheSnapshot = this.cache?.PerformanceCounter.Snapshot();
            this.dateTimeProvider = dateTimeProvider;
        }

        public bool CanLog
        {
            get
            {
                return (this.dateTimeProvider.GetUtcNow() - this.lastRepositorySnapshot.Taken) > TimeSpan.FromSeconds(10.0);
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