using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Statistics to measure the hit rate of the cache.
    /// </summary>
    public class CachePerformanceCounter
    {
        /// <summary>UTC timestamp when the performance counter was created.</summary>
        private DateTime start;

        /// <summary>UTC timestamp when the performance counter was created.</summary>
        public DateTime Start
        {
            get { return this.start; }
        }

        /// <summary>Number of cache queries for which the result was not found in the cache.</summary>
        private long missCount;

        /// <summary>Number of cache queries for which the result was not found in the cache.</summary>
        public long MissCount
        {
            get { return this.missCount; }
        }

        /// <summary>Number of cache queries for which the result was found in the cache.</summary>
        private long hitCount;

        /// <summary>Number of cache queries for which the result was found in the cache.</summary>
        public long HitCount
        {
            get { return this.hitCount; }
        }

        /// <summary>Time span since the performance counter was created.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                return this.dateTimeProvider.GetUtcNow() - this.Start;
            }
        }

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of date time functionality.</param>
        public CachePerformanceCounter(IDateTimeProvider dateTimeProvider)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.dateTimeProvider = dateTimeProvider;
            this.start = this.dateTimeProvider.GetUtcNow();
        }

        /// <summary>
        /// Adds new sample to the number of missed cache queries.
        /// </summary>
        /// <param name="count">Number of missed queries to add.</param>
        [NoTrace]
        public void AddMissCount(long count)
        {
            Interlocked.Add(ref this.missCount, count);
        }

        /// <summary>
        /// Adds new sample to the number of hit cache queries.
        /// </summary>
        /// <param name="count">Number of hit queries to add.</param>
        [NoTrace]
        public void AddHitCount(long count)
        {
            Interlocked.Add(ref this.hitCount, count);
        }

        /// <summary>
        /// Creates a snapshot of the current state of the performance counter.
        /// </summary>
        /// <returns>Newly created snapshot.</returns>
        [NoTrace]
        public CachePerformanceSnapshot Snapshot()
        {
            var snap = new CachePerformanceSnapshot(this.missCount, this.hitCount)
            {
                Start = this.Start,
                // TODO: Would it not be better for these two guys to be part of the constructor? Either implicitly or explicitly.
                Taken = this.dateTimeProvider.GetUtcNow()
            };
            return snap;
        }
    }

    /// <summary>
    /// Snapshot of a state of a performance counter taken at a certain time.
    /// </summary>
    public class CachePerformanceSnapshot
    {
        /// <summary>Number of cache queries for which the result was found in the cache.</summary>
        private readonly long hitCount;

        /// <summary>Number of cache queries for which the result was found in the cache.</summary>
        public long TotalHitCount
        {
            get { return this.hitCount; }
        }

        /// <summary>Number of cache queries for which the result was not found in the cache.</summary>
        private readonly long missCount;

        /// <summary>Number of cache queries for which the result was not found in the cache.</summary>
        public long TotalMissCount
        {
            get { return this.missCount; }
        }

        /// <summary>UTC timestamp when the snapshotted performance counter was created.</summary>
        public DateTime Start { get; internal set; }

        /// <summary>UTC timestamp when the snapshot was taken.</summary>
        public DateTime Taken { get; internal set; }

        /// <summary>Time span between the creation of the performance counter and the creation of its snapshot.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="missCount">Number of cache queries for which the result was not found in the cache.</param>
        /// <param name="hitCount">Number of cache queries for which the result was found in the cache.</param>
        [NoTrace]
        public CachePerformanceSnapshot(long missCount, long hitCount)
        {
            this.missCount = missCount;
            this.hitCount = hitCount;
        }

        /// <summary>
        /// Creates a snapshot based on difference of two performance counter snapshots.
        /// <para>
        /// This is used to obtain statistic information about performance of the cache
        /// during certain period.</para>
        /// </summary>
        /// <param name="end">Newer performance counter snapshot.</param>
        /// <param name="start">Older performance counter snapshot.</param>
        /// <returns>Snapshot of the difference between the two performance counter snapshots.</returns>
        /// <remarks>The two snapshots should be taken from a single performance counter.
        /// Otherwise the start times of the snapshots will be different, which is not allowed.</remarks>
        [NoTrace]
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

        /// <inheritdoc />
        [NoTrace]
        public override string ToString()
        {
            long total = this.TotalMissCount + this.TotalHitCount;
            var builder = new StringBuilder();
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
