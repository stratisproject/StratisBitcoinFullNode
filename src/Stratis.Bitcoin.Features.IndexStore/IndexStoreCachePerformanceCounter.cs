using System;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreCachePerformanceCounter : BlockStoreCachePerformanceCounter
    {
        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of date time functionality.</param>
        public IndexStoreCachePerformanceCounter(IDateTimeProvider dateTimeProvider) :
            base(dateTimeProvider, "IndexStore")
        {
        }

        public override BlockStoreCachePerformanceSnapshot Snapshot()
        {
            var snap = new IndexStoreCachePerformanceSnapshot(this.CacheHitCount, this.CacheMissCount, this.CacheRemoveCount, this.CacheSetCount)
            {
                Start = this.Start,
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }
    }

    public class IndexStoreCachePerformanceSnapshot : BlockStoreCachePerformanceSnapshot
    {
        public IndexStoreCachePerformanceSnapshot(long cacheHitCount, long cacheMissCount, long cacheRemoveCount, long cacheSetCount)
            : base(cacheHitCount, cacheMissCount, cacheRemoveCount, cacheSetCount, "IndexStore")
        {
        }

        public static IndexStoreCachePerformanceSnapshot operator -(IndexStoreCachePerformanceSnapshot end, IndexStoreCachePerformanceSnapshot start)
        {
            var diff = (end as BlockStoreCachePerformanceSnapshot) - (start as BlockStoreCachePerformanceSnapshot);

            return new IndexStoreCachePerformanceSnapshot(diff.TotalCacheHitCount, diff.TotalCacheMissCount, diff.TotalCacheRemoveCount, diff.TotalCacheSetCount)
            {
                Start = diff.Start,
                Taken = diff.Taken
            };    
        }
    }
}
