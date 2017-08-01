using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.BlockStore
{
	public class BlockStoreRepositoryPerformanceCounter
	{
		private DateTime _Start;
		private long _RepositoryInsertCount;
		private long _RepositoryDeleteCount;
		private long _RepositoryHitCount;
		private long _RepositoryMissCount;

		public BlockStoreRepositoryPerformanceCounter()
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

		public long RepositoryInsertCount
		{
			get
			{
				return this._RepositoryInsertCount;
			}
		}

		public long RepositoryDeleteCount
		{
			get
			{
				return this._RepositoryDeleteCount;
			}
		}

		public long RepositoryHitCount
		{
			get
			{
				return this._RepositoryHitCount;
			}
		}

		public long RepositoryMissCount
		{
			get
			{
				return this._RepositoryMissCount;
			}
		}

		internal void AddRepositoryHitCount(long count)
		{
			Interlocked.Add(ref this._RepositoryHitCount, count);
		}

		internal void AddRepositoryMissCount(long count)
		{
			Interlocked.Add(ref this._RepositoryMissCount, count);
		}

		internal void AddRepositoryDeleteCount(long count)
		{
			Interlocked.Add(ref this._RepositoryDeleteCount, count);
		}

		internal void AddRepositoryInsertCount(long count)
		{
			Interlocked.Add(ref this._RepositoryInsertCount, count);
		}

		public BlockStoreRepositoryPerformanceSnapshot Snapshot()
		{
#if !(PORTABLE || NETCORE)
			Thread.MemoryBarrier();
#endif
			var snap = new BlockStoreRepositoryPerformanceSnapshot(this.RepositoryHitCount, this.RepositoryMissCount, this.RepositoryDeleteCount, this.RepositoryInsertCount)
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

	public class BlockStoreRepositoryPerformanceSnapshot
	{
		private readonly long _RepositoryHitCount;
		private readonly long _RepositoryMissCount;
		private readonly long _RepositoryDeleteCount;
		private readonly long _RepositoryInsertCount;

		public BlockStoreRepositoryPerformanceSnapshot(long repositoryHitCount, long repositoryMissCount, long repositoryDeleteCount, long repositoryInsertCount)
		{
			this._RepositoryHitCount = repositoryHitCount;
			this._RepositoryMissCount = repositoryMissCount;
			this._RepositoryDeleteCount = repositoryDeleteCount;
            this._RepositoryInsertCount = repositoryInsertCount;
		}

		public long TotalRepositoryHitCount
		{
			get
			{
				return this._RepositoryHitCount;
			}
		}

		public long TotalRepositoryMissCount
		{
			get
			{
				return this._RepositoryMissCount;
			}
		}

		public long TotalRepositoryDeleteCount
		{
			get
			{
				return this._RepositoryDeleteCount;
			}
		}

		public long TotalRepositoryInsertCount
		{
			get
			{
				return this._RepositoryInsertCount;
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

        public static BlockStoreRepositoryPerformanceSnapshot operator -(BlockStoreRepositoryPerformanceSnapshot end, BlockStoreRepositoryPerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }
            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }
            return new BlockStoreRepositoryPerformanceSnapshot(end.TotalRepositoryHitCount - start.TotalRepositoryHitCount,
                                            end.TotalRepositoryMissCount - start.TotalRepositoryMissCount,
                                            end.TotalRepositoryDeleteCount - start.TotalRepositoryDeleteCount,
                                            end.TotalRepositoryInsertCount - start.TotalRepositoryInsertCount)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendLine("====BlockStore Repository Stats(%)====");
			builder.AppendLine("Hit Count:".PadRight(LogsExtension.ColumnLength) + this.TotalRepositoryHitCount);
			builder.AppendLine("Miss Count:".PadRight(LogsExtension.ColumnLength) + this.TotalRepositoryMissCount);
			builder.AppendLine("Delete Count:".PadRight(LogsExtension.ColumnLength) + this.TotalRepositoryDeleteCount);
			builder.AppendLine("Insert Count:".PadRight(LogsExtension.ColumnLength) + this.TotalRepositoryInsertCount);

			var total = this.TotalRepositoryMissCount + this.TotalRepositoryHitCount;
			if (this.TotalRepositoryHitCount > 0 || this.TotalRepositoryMissCount > 0)
			{
				builder.AppendLine("Hit:".PadRight(LogsExtension.ColumnLength) + ((decimal)this.TotalRepositoryHitCount * 100m / total).ToString("0.00") + " %");
				builder.AppendLine("Miss:".PadRight(LogsExtension.ColumnLength) + ((decimal)this.TotalRepositoryMissCount * 100m / total).ToString("0.00") + " %");
			}
			builder.AppendLine("=================================");

			return builder.ToString();
		}
	}
}
