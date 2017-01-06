using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class BackendPerformanceSnapshot
	{
		public BackendPerformanceSnapshot(long insertedEntities, long insertTime,
										  long queriedEntities, long queryTime)
		{
			_TotalInsertedEntities = insertedEntities;
			_TotalInsertTime = insertTime;
			_TotalQueryTime = queryTime;
			_TotalQueriedEntities = queriedEntities;
		}


		private readonly long _TotalQueriedEntities;
		public long TotalQueriedEntities
		{
			get
			{
				return _TotalQueriedEntities;
			}
		}

		private readonly long _TotalQueryTime;
		public TimeSpan TotalQueryTime
		{
			get
			{
				return TimeSpan.FromTicks(_TotalQueryTime);
			}
		}

		private readonly long _TotalInsertTime;
		public TimeSpan TotalInsertTime
		{
			get
			{
				return TimeSpan.FromTicks(_TotalInsertTime);
			}
		}

		readonly long _TotalInsertedEntities;
		public long TotalInsertedEntities
		{
			get
			{
				return _TotalInsertedEntities;
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
			if(end.Start != start.Start)
			{
				throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
			}
			if(end.Taken < start.Taken)
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
				return Taken - Start;
			}
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			if(TotalInsertedEntities > 0)
				builder.AppendLine("Insert speed:".PadRight(Logs.ColumnLength) + (TotalInsertTime.TotalMilliseconds / TotalInsertedEntities).ToString("0.0000") + " ms/utxo");
			builder.AppendLine("Insert time:".PadRight(Logs.ColumnLength) + ConsensusPerformanceSnapshot.ToTimespan(TotalInsertTime));
			builder.AppendLine("Inserted UTXO:".PadRight(Logs.ColumnLength) + TotalInsertedEntities);
			if(TotalQueriedEntities > 0)
				builder.AppendLine("Query speed:".PadRight(Logs.ColumnLength) + (TotalQueryTime.TotalMilliseconds / TotalQueriedEntities).ToString("0.0000") + " ms/utxo");
			builder.AppendLine("Query time:".PadRight(Logs.ColumnLength) + ConsensusPerformanceSnapshot.ToTimespan(TotalQueryTime));
			builder.AppendLine("Queried UTXO:".PadRight(Logs.ColumnLength) + TotalQueriedEntities);
			return builder.ToString();
		}
	}
	public class BackendPerformanceCounter
	{
		public BackendPerformanceCounter()
		{
			_Start = DateTime.UtcNow;
		}

		DateTime _Start;
		public DateTime Start
		{
			get
			{
				return _Start;
			}
		}
		public TimeSpan Elapsed
		{
			get
			{
				return DateTime.UtcNow - Start;
			}
		}

		public override string ToString()
		{
			return Snapshot().ToString();
		}

		public void AddInsertTime(long count)
		{
			Interlocked.Add(ref _InsertTime, count);
		}
		private long _InsertTime;
		public TimeSpan InsertTime
		{
			get
			{
				return TimeSpan.FromTicks(_InsertTime);
			}
		}

		public void AddInsertedEntities(long count)
		{
			Interlocked.Add(ref _InsertedEntities, count);
		}
		private long _InsertedEntities;
		public long InsertedEntities
		{
			get
			{
				return _InsertedEntities;
			}
		}

		public void AddQueryTime(long count)
		{
			Interlocked.Add(ref _QueryTime, count);
		}

		private long _QueryTime;
		public TimeSpan QueryTime
		{
			get
			{
				return TimeSpan.FromTicks(_QueryTime);
			}
		}

		public void AddQueriedEntities(long count)
		{
			Interlocked.Add(ref _QueriedEntities, count);
		}
		private long _QueriedEntities;
		public long QueriedEntities
		{
			get
			{
				return _QueriedEntities;
			}
		}

		public BackendPerformanceSnapshot Snapshot()
		{
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
			var snap = new BackendPerformanceSnapshot(_InsertedEntities, _InsertTime, _QueriedEntities, _QueryTime)
			{
				Start = Start,
				Taken = DateTime.UtcNow
			};
			return snap;
		}
	}
}
