﻿using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreCachePerformanceCounter
    {
        private long cacheSetCount;

        private long cacheRemoveCount;

        private long cacheHitCount;

        private long cacheMissCount;

        public string Name { get; private set; }

        public DateTime Start { get; private set; }

        /// <summary>Provider of date time functionality.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        public BlockStoreCachePerformanceCounter(IDateTimeProvider dateTimeProvider, string name = "BlockStore")
        {
            this.Name = name;
            this.dateTimeProvider = dateTimeProvider;
            this.Start = this.dateTimeProvider.GetUtcNow();
        }

        public TimeSpan Elapsed
        {
            get
            {
                return this.dateTimeProvider.GetUtcNow() - this.Start;
            }
        }

        public long CacheSetCount
        {
            get
            {
                return this.cacheSetCount;
            }
        }

        public long CacheRemoveCount
        {
            get
            {
                return this.cacheRemoveCount;
            }
        }

        public long CacheHitCount
        {
            get
            {
                return this.cacheHitCount;
            }
        }

        public long CacheMissCount
        {
            get
            {
                return this.cacheMissCount;
            }
        }

        public void AddCacheHitCount(long count)
        {
            Interlocked.Add(ref this.cacheHitCount, count);
        }

        public void AddCacheRemoveCount(long count)
        {
            Interlocked.Add(ref this.cacheRemoveCount, count);
        }

        public void AddCacheMissCount(long count)
        {
            Interlocked.Add(ref this.cacheMissCount, count);
        }

        public void AddCacheSetCount(long count)
        {
            Interlocked.Add(ref this.cacheSetCount, count);
        }

        public virtual BlockStoreCachePerformanceSnapshot Snapshot()
        {
            var snap = new BlockStoreCachePerformanceSnapshot(this.CacheHitCount, this.CacheMissCount, this.CacheRemoveCount, this.CacheSetCount, this.Name)
            {
                Start = this.Start,
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }

        public override string ToString()
        {
            return this.Snapshot().ToString();
        }
    }

    public class BlockStoreCachePerformanceSnapshot
    {
        public long TotalCacheHitCount { get; }

        public long TotalCacheMissCount { get; }

        public long TotalCacheRemoveCount { get; }

        public long TotalCacheSetCount { get; }

        public string Name { get; private set; }

        public DateTime Start { get; set; }

        public DateTime Taken { get; set; }

        public BlockStoreCachePerformanceSnapshot(long cacheHitCount, long cacheMissCount, long cacheRemoveCount, long cacheSetCount, string name = "BlockStore")
        {
            this.TotalCacheHitCount = cacheHitCount;
            this.TotalCacheMissCount = cacheMissCount;
            this.TotalCacheRemoveCount = cacheRemoveCount;
            this.TotalCacheSetCount = cacheSetCount;
            this.Name = name;
        }
        
        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public static BlockStoreCachePerformanceSnapshot operator -(BlockStoreCachePerformanceSnapshot end, BlockStoreCachePerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new BlockStoreCachePerformanceSnapshot(end.TotalCacheHitCount - start.TotalCacheHitCount,
                                            end.TotalCacheMissCount - start.TotalCacheMissCount,
                                            end.TotalCacheRemoveCount - start.TotalCacheRemoveCount,
                                            end.TotalCacheSetCount - start.TotalCacheSetCount, start.Name)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"===={this.Name} Cache Stats(%)====");
            builder.AppendLine("Hit Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheHitCount);
            builder.AppendLine("Miss Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheMissCount);
            builder.AppendLine("Remove Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheRemoveCount);
            builder.AppendLine("Set Count:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalCacheSetCount);

            long total = this.TotalCacheMissCount + this.TotalCacheHitCount;
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
