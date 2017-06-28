using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using System;
using System.Text;

namespace Stratis.Bitcoin.BlockStore
{
    public class BlockStoreStats
    {
        private BlockRepository repository;
        private BlockStoreCache cache;
        private BlockStoreRepositoryPerformanceSnapshot lastRepositorySnapshot;
        private BlockStoreCachePerformanceSnapshot lastCacheSnapshot;

        public BlockStoreStats(BlockRepository blockRepository, BlockStoreCache blockStoreCache)
        {
            this.repository = blockRepository;
            this.cache = blockStoreCache;
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

            Logs.BlockStore.LogInformation(performanceLogBuilder.ToString());
        }       
    }
}
