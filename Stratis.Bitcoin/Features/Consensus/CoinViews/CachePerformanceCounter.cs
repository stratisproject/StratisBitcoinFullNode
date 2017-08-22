using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class CachePerformanceCounter
    {
        /// TODO: Change to IDateTimeProvider.
        DateTime start;
        /// TODO: Change to IDateTimeProvider.
        public DateTime Start { get { return this.start; } }

        private long missCount;
        public long MissCount { get { return this.missCount; } }

        private long hitCount;
        public long HitCount { get { return this.hitCount; } }

        public TimeSpan Elapsed
        {
            get
            {
                // TODO: Change to IDateTimeProvider.
                return DateTime.UtcNow - this.Start;
            }
        }

        public CachePerformanceCounter()
        {
            // TODO: Change to IDateTimeProvider.
            this.start = DateTime.UtcNow;
        }

        public void AddMissCount(long count)
        {
            Interlocked.Add(ref this.missCount, count);
        }

        public void AddHitCount(long count)
        {
            Interlocked.Add(ref this.hitCount, count);
        }

        public CachePerformanceSnapshot Snapshot()
        {
#if !(PORTABLE || NETCORE)
            Thread.MemoryBarrier();
#endif
            var snap = new CachePerformanceSnapshot(this.missCount, this.hitCount)
            {
                Start = this.Start,
                // TODO: Change to IDateTimeProvider.
                Taken = DateTime.UtcNow
            };
            return snap;
        }
    }

    public class CachePerformanceSnapshot
    {
        private readonly long hitCount;
        private readonly long missCount;

        public long TotalHitCount { get { return this.hitCount; } }

        public long TotalMissCount { get { return this.missCount; } }

        // TODO: Change to IDateTimeProvider.
        public DateTime Start { get; internal set; }

        // TODO: Change to IDateTimeProvider.
        public DateTime Taken { get; internal set; }

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public CachePerformanceSnapshot(long missCount, long hitCount)
        {
            this.missCount = missCount;
            this.hitCount = hitCount;
        }

        public static CachePerformanceSnapshot operator -(CachePerformanceSnapshot end, CachePerformanceSnapshot start)
        {
            if (end.Start != start.Start)
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");

            if (end.Taken < start.Taken)
                throw new InvalidOperationException("The difference of snapshot can't be negative");

            long missCount = end.missCount - start.missCount;
            long hitCount = end.hitCount - start.hitCount;
            return new CachePerformanceSnapshot(missCount, hitCount)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            long total = this.TotalMissCount + this.TotalHitCount;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("====Cache Stats(%)====");
            if (total != 0)
            {
                builder.AppendLine("Hit:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalHitCount * 100m / total).ToString("0.00") + " %");
                builder.AppendLine("Miss:".PadRight(LoggingConfiguration.ColumnLength) + ((decimal)this.TotalMissCount * 100m / total).ToString("0.00") + " %");
            }

            builder.AppendLine("========================");
            return builder.ToString();
        }
    }
}
