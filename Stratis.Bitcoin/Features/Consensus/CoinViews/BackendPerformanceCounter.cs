using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Performance statistics used to measure the time it takes for the DBreeze backend 
    /// to perform database operations.
    /// </summary>
    public class BackendPerformanceCounter
    {
        /// <summary>UTC timestamp when the performance counter was created.</summary>
        // TODO: Change to IDateTimeProvider.
        private readonly DateTime start;
        /// <summary>UTC timestamp when the performance counter was created.</summary>
        // TODO: Change to IDateTimeProvider.
        public DateTime Start { get { return this.start; } }

        /// <summary>Number of entities inserted to the database.</summary>
        private long insertedEntities;
        /// <summary>Number of entities inserted to the database.</summary>
        public long InsertedEntities { get { return this.insertedEntities; } }

        /// <summary>Number of queried entities from the database.</summary>
        private long queriedEntities;
        /// <summary>Number of queried entities from the database.</summary>
        public long QueriedEntities { get { return this.queriedEntities; } }

        /// <summary>Time span since the performance counter was created.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                // TODO: Change to IDateTimeProvider.</remarks>
                return DateTime.UtcNow - this.Start;
            }
        }

        /// <summary>Time in ticks it took the database to perform the insert operation.</summary>
        private long insertTime;
        /// <summary>Time it took the database to perform the insert operation.</summary>
        public TimeSpan InsertTime
        {
            get
            {
                return TimeSpan.FromTicks(this.insertTime);
            }
        }

        /// <summary>Time in ticks it took the database to perform a query operation.</summary>
        private long queryTime;
        /// <summary>Time it took the database to perform a query operation.</summary>
        public TimeSpan QueryTime
        {
            get
            {
                return TimeSpan.FromTicks(this.queryTime);
            }
        }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public BackendPerformanceCounter()
        {
            // TODO: Change to IDateTimeProvider.
            this.start = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Snapshot().ToString();
        }

        public void AddInsertTime(long count)
        {
            Interlocked.Add(ref this.insertTime, count);
        }

        public void AddInsertedEntities(long count)
        {
            Interlocked.Add(ref this.insertedEntities, count);
        }

        public void AddQueryTime(long count)
        {
            Interlocked.Add(ref this.queryTime, count);
        }

        public void AddQueriedEntities(long count)
        {
            Interlocked.Add(ref this.queriedEntities, count);
        }

        /// <summary>
        /// Creates a snapshot of the current state of the performance counter.
        /// </summary>
        /// <returns>Newly created snapshot.</returns>
        public BackendPerformanceSnapshot Snapshot()
        {
            var snap = new BackendPerformanceSnapshot(this.insertedEntities, this.insertTime, this.queriedEntities, this.queryTime)
            {
                Start = this.Start,
                // TODO: Change to IDateTimeProvider.
                Taken = DateTime.UtcNow
            };
            return snap;
        }
    }

    /// <summary>
    /// Snapshot of a state of a performance counter taken at a certain time.
    /// </summary>
    public class BackendPerformanceSnapshot
    {
        private readonly long totalQueriedEntities;
        public long TotalQueriedEntities { get { return this.totalQueriedEntities; } }

        private readonly long totalQueryTime;
        public TimeSpan TotalQueryTime { get { return TimeSpan.FromTicks(this.totalQueryTime); } }

        private readonly long totalInsertTime;
        public TimeSpan TotalInsertTime { get { return TimeSpan.FromTicks(this.totalInsertTime); } }

        readonly long totalInsertedEntities;
        public long TotalInsertedEntities { get { return this.totalInsertedEntities; } }

        // TODO: Change to IDateTimeProvider.
        public DateTime Start { get; set; }

        // TODO: Change to IDateTimeProvider.
        public DateTime Taken { get; set; }

        public BackendPerformanceSnapshot(long insertedEntities, long insertTime, long queriedEntities, long queryTime)
        {
            this.totalInsertedEntities = insertedEntities;
            this.totalInsertTime = insertTime;
            this.totalQueryTime = queryTime;
            this.totalQueriedEntities = queriedEntities;
        }

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

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if (this.TotalInsertedEntities > 0)
                builder.AppendLine("Insert speed:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalInsertTime.TotalMilliseconds / this.TotalInsertedEntities).ToString("0.0000") + " ms/utxo");

            builder.AppendLine("Insert time:".PadRight(LoggingConfiguration.ColumnLength) + ConsensusPerformanceSnapshot.ToTimespan(this.TotalInsertTime));
            builder.AppendLine("Inserted UTXO:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalInsertedEntities);

            if (this.TotalQueriedEntities > 0)
                builder.AppendLine("Query speed:".PadRight(LoggingConfiguration.ColumnLength) + (this.TotalQueryTime.TotalMilliseconds / this.TotalQueriedEntities).ToString("0.0000") + " ms/utxo");

            builder.AppendLine("Query time:".PadRight(LoggingConfiguration.ColumnLength) + ConsensusPerformanceSnapshot.ToTimespan(this.TotalQueryTime));
            builder.AppendLine("Queried UTXO:".PadRight(LoggingConfiguration.ColumnLength) + this.TotalQueriedEntities);
            return builder.ToString();
        }
    }
}
