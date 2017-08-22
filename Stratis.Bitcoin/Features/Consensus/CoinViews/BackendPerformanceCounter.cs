using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class BackendPerformanceSnapshot
    {
        public BackendPerformanceSnapshot(long insertedEntities, long insertTime, long queriedEntities, long queryTime)
        {
            this._TotalInsertedEntities = insertedEntities;
            this._TotalInsertTime = insertTime;
            this._TotalQueryTime = queryTime;
            this._TotalQueriedEntities = queriedEntities;
        }

        private readonly long _TotalQueriedEntities;
        public long TotalQueriedEntities
        {
            get
            {
                return this._TotalQueriedEntities;
            }
        }

        private readonly long _TotalQueryTime;
        public TimeSpan TotalQueryTime
        {
            get
            {
                return TimeSpan.FromTicks(this._TotalQueryTime);
            }
        }

        private readonly long _TotalInsertTime;
        public TimeSpan TotalInsertTime
        {
            get
            {
                return TimeSpan.FromTicks(this._TotalInsertTime);
            }
        }

        readonly long _TotalInsertedEntities;
        public long TotalInsertedEntities
        {
            get
            {
                return this._TotalInsertedEntities;
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

        public static BackendPerformanceSnapshot operator -(BackendPerformanceSnapshot end, BackendPerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new BackendPerformanceSnapshot(end._TotalInsertedEntities - start._TotalInsertedEntities,
                                            end._TotalInsertTime - start._TotalInsertTime,
                                            end._TotalQueriedEntities - start._TotalQueriedEntities,
                                            end._TotalQueryTime - start._TotalQueryTime)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
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
    public class BackendPerformanceCounter
    {
        public BackendPerformanceCounter()
        {
            this._Start = DateTime.UtcNow;
        }

        DateTime _Start;
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

        public override string ToString()
        {
            return this.Snapshot().ToString();
        }

        public void AddInsertTime(long count)
        {
            Interlocked.Add(ref this._InsertTime, count);
        }
        private long _InsertTime;
        public TimeSpan InsertTime
        {
            get
            {
                return TimeSpan.FromTicks(this._InsertTime);
            }
        }

        public void AddInsertedEntities(long count)
        {
            Interlocked.Add(ref this._InsertedEntities, count);
        }
        private long _InsertedEntities;
        public long InsertedEntities
        {
            get
            {
                return this._InsertedEntities;
            }
        }

        public void AddQueryTime(long count)
        {
            Interlocked.Add(ref this._QueryTime, count);
        }

        private long _QueryTime;
        public TimeSpan QueryTime
        {
            get
            {
                return TimeSpan.FromTicks(this._QueryTime);
            }
        }

        public void AddQueriedEntities(long count)
        {
            Interlocked.Add(ref this._QueriedEntities, count);
        }
        private long _QueriedEntities;
        public long QueriedEntities
        {
            get
            {
                return this._QueriedEntities;
            }
        }

        public BackendPerformanceSnapshot Snapshot()
        {
#if !(PORTABLE || NETCORE)
            Thread.MemoryBarrier();
#endif
            var snap = new BackendPerformanceSnapshot(this._InsertedEntities, this._InsertTime, this._QueriedEntities, this._QueryTime)
            {
                Start = this.Start,
                Taken = DateTime.UtcNow
            };
            return snap;
        }
    }
}
