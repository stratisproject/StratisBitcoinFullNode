using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreCachePerformanceCounter
    {
        private DateTime _Start;
        private long _CacheSetCount;
        private long _CacheRemoveCount;
        private long _CacheHitCount;
        private long _CacheMissCount;

        public IndexStoreCachePerformanceCounter()
        {
            this._Start = DateTime.UtcNow;
        }

        public DateTime Start
        {
            get
            {
                return this._Start;
            }
        }
        public TimeSpan Elapsed
        {
            get
            {
                return DateTime.UtcNow - this.Start;
            }
        }


        public long CacheSetCount
        {
            get
            {
                return this._CacheSetCount;
            }
        }

        public long CacheRemoveCount
        {
            get
            {
                return this._CacheRemoveCount;
            }
        }


        public long CacheHitCount
        {
            get
            {
                return this._CacheHitCount;
            }
        }

        public long CacheMissCount
        {
            get
            {
                return this._CacheMissCount;
            }
        }

        internal void AddCacheHitCount(long count)
        {
            Interlocked.Add(ref this._CacheHitCount, count);
        }

        internal void AddCacheRemoveCount(long count)
        {
            Interlocked.Add(ref this._CacheRemoveCount, count);
        }

        internal void AddCacheMissCount(long count)
        {
            Interlocked.Add(ref this._CacheMissCount, count);
        }

        internal void AddCacheSetCount(long count)
        {
            Interlocked.Add(ref this._CacheSetCount, count);
        }

        public IndexStoreCachePerformanceSnapshot Snapshot()
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

        public override string ToString()
        {
            return this.Snapshot().ToString();
        }
    }

    public class IndexStoreCachePerformanceSnapshot
    {
        private readonly long _CacheHitCount;
        private readonly long _CacheMissCount;
        private readonly long _CacheRemoveCount;
        private readonly long _CacheSetCount;

        public IndexStoreCachePerformanceSnapshot(long cacheHitCount, long cacheMissCount, long cacheRemoveCount, long cacheSetCount)
        {
            this._CacheHitCount = cacheHitCount;
            this._CacheMissCount = cacheMissCount;
            this._CacheRemoveCount = cacheRemoveCount;
            this._CacheSetCount = cacheSetCount;
        }

        public long TotalCacheHitCount
        {
            get
            {
                return this._CacheHitCount;
            }
        }

        public long TotalCacheMissCount
        {
            get
            {
                return this._CacheMissCount;
            }
        }

        public long TotalCacheRemoveCount
        {
            get
            {
                return this._CacheRemoveCount;
            }
        }

        public long TotalCacheSetCount
        {
            get
            {
                return this._CacheSetCount;
            }
        }

        public DateTime Start
        {
            get;
            set;
        }

        public DateTime Taken
        {
            get;
            set;
        }

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public static IndexStoreCachePerformanceSnapshot operator -(IndexStoreCachePerformanceSnapshot end, IndexStoreCachePerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new IndexStoreCachePerformanceSnapshot(end.TotalCacheHitCount - start.TotalCacheHitCount,
                                            end.TotalCacheMissCount - start.TotalCacheMissCount,
                                            end.TotalCacheRemoveCount - start.TotalCacheRemoveCount,
                                            end.TotalCacheSetCount - start.TotalCacheSetCount)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("====IndexStore Cache Stats(%)====");
            builder.AppendLine("Hit Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheHitCount);
            builder.AppendLine("Miss Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheMissCount);
            builder.AppendLine("Remove Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheRemoveCount);
            builder.AppendLine("Set Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheSetCount);

            var total = this.TotalCacheMissCount + this.TotalCacheHitCount;
            if (this.TotalCacheHitCount > 0 || this.TotalCacheMissCount > 0)
            {
                builder.AppendLine("Hit:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalCacheHitCount * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Miss:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalCacheMissCount * 100m / total).ToString("0.00") + " %");
            }
            builder.AppendLine("=================================");

            return builder.ToString();
        }
    }
}
