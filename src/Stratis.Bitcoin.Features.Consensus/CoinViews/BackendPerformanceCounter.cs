using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Performance statistics used to measure the time it takes for the DBreeze backend
    /// to perform database operations.
    /// </summary>
    public class BackendPerformanceCounter
    {
        /// <summary>UTC timestamp when the performance counter was created.</summary>
        private readonly DateTime start;

        /// <summary>UTC timestamp when the performance counter was created.</summary>
        public DateTime Start
        {
            get { return this.start; }
        }

        /// <summary>Number of entities inserted to the database.</summary>
        private long insertedEntities;

        /// <summary>Number of entities inserted to the database.</summary>
        public long InsertedEntities
        {
            get { return this.insertedEntities; }
        }

        /// <summary>Number of queried entities from the database.</summary>
        private long queriedEntities;

        /// <summary>Number of queried entities from the database.</summary>
        public long QueriedEntities
        {
            get { return this.queriedEntities; }
        }

        /// <summary>Time span since the performance counter was created.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                return this.dateTimeProvider.GetUtcNow() - this.Start;
            }
        }

        /// <summary>Time in ticks it took the database to perform insert operations.</summary>
        private long insertTime;

        /// <summary>Time it took the database to perform insert operations.</summary>
        public TimeSpan InsertTime
        {
            get
            {
                return TimeSpan.FromTicks(this.insertTime);
            }
        }

        /// <summary>Time in ticks it took the database to perform query operations.</summary>
        private long queryTime;

        /// <summary>Time it took the database to perform query operations.</summary>
        public TimeSpan QueryTime
        {
            get
            {
                return TimeSpan.FromTicks(this.queryTime);
            }
        }

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="dateTimeProvider">The date time provider for the node.</param>
        public BackendPerformanceCounter(IDateTimeProvider dateTimeProvider)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.dateTimeProvider = dateTimeProvider;
            this.start = this.dateTimeProvider.GetUtcNow();
        }

        /// <inheritdoc />
        [NoTrace]
        public override string ToString()
        {
            return this.Snapshot().ToString();
        }

        /// <summary>
        /// Adds sample for database insert operation to the performance counter.
        /// </summary>
        /// <param name="count">Time in ticks it took the database to perform the insert operation.</param>
        [NoTrace]
        public void AddInsertTime(long count)
        {
            Interlocked.Add(ref this.insertTime, count);
        }

        /// <summary>
        /// Increases the number of inserted entities in the performance counter.
        /// </summary>
        /// <param name="count">Number of newly inserted entities to add.</param>
        [NoTrace]
        public void AddInsertedEntities(long count)
        {
            Interlocked.Add(ref this.insertedEntities, count);
        }

        /// <summary>
        /// Adds sample for database query operation to the performance counter.
        /// </summary>
        /// <param name="count">Time in ticks it took the database to perform the query operation.</param>
        [NoTrace]
        public void AddQueryTime(long count)
        {
            Interlocked.Add(ref this.queryTime, count);
        }

        /// <summary>
        /// Increases the number of queried entities in the performance counter.
        /// </summary>
        /// <param name="count">Number of newly queried entities to add.</param>
        [NoTrace]
        public void AddQueriedEntities(long count)
        {
            Interlocked.Add(ref this.queriedEntities, count);
        }

        /// <summary>
        /// Creates a snapshot of the current state of the performance counter.
        /// </summary>
        /// <returns>Newly created snapshot.</returns>
        [NoTrace]
        public BackendPerformanceSnapshot Snapshot()
        {
            var snap = new BackendPerformanceSnapshot(this.insertedEntities, this.insertTime, this.queriedEntities, this.queryTime)
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
    public class BackendPerformanceSnapshot
    {
        /// <summary>Number of queried entities from the database.</summary>
        private readonly long totalQueriedEntities;

        /// <summary>Number of queried entities from the database.</summary>
        public long TotalQueriedEntities
        {
            get { return this.totalQueriedEntities; }
        }

        /// <summary>Time in ticks it took the database to perform query operations.</summary>
        private readonly long totalQueryTime;

        /// <summary>Time it took the database to perform query operations.</summary>
        public TimeSpan TotalQueryTime
        {
            get { return TimeSpan.FromTicks(this.totalQueryTime); }
        }

        /// <summary>Time in ticks it took the database to perform insert operations.</summary>
        private readonly long totalInsertTime;

        /// <summary>Time it took the database to perform insert operations.</summary>
        public TimeSpan TotalInsertTime
        {
            get { return TimeSpan.FromTicks(this.totalInsertTime); }
        }

        /// <summary>Number of entities inserted to the database.</summary>
        internal readonly long totalInsertedEntities;

        /// <summary>Number of entities inserted to the database.</summary>
        public long TotalInsertedEntities
        {
            get { return this.totalInsertedEntities; }
        }

        /// <summary>UTC timestamp when the snapshotted performance counter was created.</summary>
        public DateTime Start { get; set; }

        /// <summary>UTC timestamp when the snapshot was taken.</summary>
        public DateTime Taken { get; set; }

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
        /// <param name="insertedEntities">Number of entities inserted to the database.</param>
        /// <param name="insertTime">Time in ticks it took the database to perform insert operations.</param>
        /// <param name="queriedEntities">Number of queried entities from the database.</param>
        /// <param name="queryTime">Time it took the database to perform query operations.</param>
        public BackendPerformanceSnapshot(long insertedEntities, long insertTime, long queriedEntities, long queryTime)
        {
            this.totalInsertedEntities = insertedEntities;
            this.totalInsertTime = insertTime;
            this.totalQueryTime = queryTime;
            this.totalQueriedEntities = queriedEntities;
        }

        /// <summary>
        /// Creates a snapshot based on difference of two performance counter snapshots.
        /// <para>
        /// This is used to obtain statistic information about performance of the backend
        /// during certain period.</para>
        /// </summary>
        /// <param name="end">Newer performance counter snapshot.</param>
        /// <param name="start">Older performance counter snapshot.</param>
        /// <returns>Snapshot of the difference between the two performance counter snapshots.</returns>
        /// <remarks>The two snapshots should be taken from a single performance counter.
        /// Otherwise the start times of the snapshots will be different, which is not allowed.</remarks>
        [NoTrace]
        public static BackendPerformanceSnapshot operator -(BackendPerformanceSnapshot end, BackendPerformanceSnapshot start)
        {
            if (end.Start != start.Start)
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");

            if (end.Taken < start.Taken)
                throw new InvalidOperationException("The difference of snapshot can't be negative");

            long insertedEntities = end.totalInsertedEntities - start.totalInsertedEntities;
            long insertTime = end.totalInsertTime - start.totalInsertTime;
            long queriedEntities = end.totalQueriedEntities - start.totalQueriedEntities;
            long queryTime = end.totalQueryTime - start.totalQueryTime;
            var snapshot = new BackendPerformanceSnapshot(insertedEntities, insertTime, queriedEntities, queryTime)
            {
                Start = start.Taken,
                Taken = end.Taken
            };

            return snapshot;
        }

        [NoTrace]
        private string TimespanToString(TimeSpan timespan)
        {
            return timespan.ToString("c");
        }

        /// <inheritdoc />
        [NoTrace]
        public override string ToString()
        {
            var builder = new StringBuilder();
            if (this.TotalInsertedEntities > 0)
                builder.AppendLine("Insert speed:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalInsertTime.TotalMilliseconds / this.TotalInsertedEntities).ToString("0.0000") + " ms/utxo");

            builder.AppendLine("Insert time:".PadRight(LoggingConfiguration.ColumnLength) + this.TimespanToString(this.TotalInsertTime));
            builder.AppendLine("Inserted UTXO:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalInsertedEntities);

            if (this.TotalQueriedEntities > 0)
                builder.AppendLine("Query speed:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalQueryTime.TotalMilliseconds / this.TotalQueriedEntities).ToString("0.0000") + " ms/utxo");

            builder.AppendLine("Query time:".PadRight(LoggingConfiguration.ColumnLength) + this.TimespanToString(this.TotalQueryTime));
            builder.AppendLine("Queried UTXO:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalQueriedEntities);
            return builder.ToString();
        }
    }
}
