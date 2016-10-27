using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class BackendPerformanceSnapshot
	{
		public BackendPerformanceSnapshot(long insertedEntities, long insertTime)
		{
			_TotalInsertedEntities = insertedEntities;
			_TotalInsertTime = insertTime;
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
											end._TotalInsertedEntities - start._TotalInsertTime)
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
			builder.AppendLine("Insert speed : " + (TotalInsertTime.TotalMilliseconds / TotalInsertedEntities).ToString("0.0000") + " ms/entities");
			builder.AppendLine("Inserted entities : " + TotalInsertedEntities);
			builder.AppendLine("Insert time : " + ConsensusPerformanceSnapshot.ToTimespan(TotalInsertTime));
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

		public BackendPerformanceSnapshot Snapshot()
		{
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
			var snap = new BackendPerformanceSnapshot(_InsertedEntities, _InsertTime)
			{
				Start = Start,
				Taken = DateTime.UtcNow
			};
			return snap;
		}
	}
}
