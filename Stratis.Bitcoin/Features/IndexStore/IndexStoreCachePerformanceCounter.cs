using System;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreCachePerformanceCounter : BlockStoreCachePerformanceCounter
    {
        public IndexStoreCachePerformanceCounter():
            base("IndexStore")
        {

        }
        public override BlockStoreCachePerformanceSnapshot Snapshot()
        {
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
            var snap = new IndexStoreCachePerformanceSnapshot(this.CacheHitCount, this.CacheMissCount, this.CacheRemoveCount, this.CacheSetCount)
            {
                Start = this.Start,
                Taken = DateTime.UtcNow
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
