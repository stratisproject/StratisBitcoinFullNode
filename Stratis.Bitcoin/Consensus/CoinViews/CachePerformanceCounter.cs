using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class CachePerformanceCounter
	{
		public CachePerformanceCounter()
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

		public void AddMissCount(long count)
		{
			Interlocked.Add(ref this._MissCount, count);
		}
		private long _MissCount;
		public long MissCount
		{
			get
			{
				return this._MissCount;
			}
		}
		public void AddHitCount(long count)
		{
			Interlocked.Add(ref this._HitCount, count);
		}
		private long _HitCount;
		public long HitCount
		{
			get
			{
				return this._HitCount;
			}
		}

		public CachePerformanceSnapshot Snapshot()
		{
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
			var snap = new CachePerformanceSnapshot(this._MissCount, this._HitCount)
			{
				Start = this.Start,
				Taken = DateTime.UtcNow
			};
			return snap;
		}
	}

	public class CachePerformanceSnapshot
	{
		private readonly long _HitCount;
		private readonly long _MissCount;

		public CachePerformanceSnapshot(long missCount, long hitCount)
		{
			this._MissCount = missCount;
			this._HitCount = hitCount;
		}

		public long TotalHitCount
		{
			get
			{
				return this._HitCount;
			}
		}

		public long TotalMissCount
		{
			get
			{
				return this._MissCount;
			}
		}

		public DateTime Start
		{
			get;
			internal set;
		}
		public DateTime Taken
		{
			get;
			internal set;
		}

		public static CachePerformanceSnapshot operator -(CachePerformanceSnapshot end, CachePerformanceSnapshot start)
		{
			if(end.Start != start.Start)
			{
				throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
			}
			if(end.Taken < start.Taken)
			{
				throw new InvalidOperationException("The difference of snapshot can't be negative");
			}
			return new CachePerformanceSnapshot(end._MissCount - start._MissCount,
											end._HitCount - start._HitCount)
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
			var total = this.TotalMissCount + this.TotalHitCount;
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("====Cache Stats(%)====");
			if(total != 0)
			{
				builder.AppendLine("Hit:".PadRight(LogsExtension.ColumnLength) + ((decimal)this.TotalHitCount * 100m / total).ToString("0.00") + " %");
				builder.AppendLine("Miss:".PadRight(LogsExtension.ColumnLength) + ((decimal)this.TotalMissCount * 100m / total).ToString("0.00") + " %");
			}
			builder.AppendLine("========================");
			return builder.ToString();
		}
	}
}
