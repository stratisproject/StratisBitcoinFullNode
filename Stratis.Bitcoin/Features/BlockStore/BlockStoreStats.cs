using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreStats
    {
        private BlockRepository repository;
        private BlockStoreCache cache;
        private readonly ILogger logger;
        private BlockStoreRepositoryPerformanceSnapshot lastRepositorySnapshot;
        private BlockStoreCachePerformanceSnapshot lastCacheSnapshot;

        public BlockStoreStats(BlockRepository blockRepository, BlockStoreCache blockStoreCache, ILogger logger)
        {
            this.repository = blockRepository;
            this.cache = blockStoreCache;
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