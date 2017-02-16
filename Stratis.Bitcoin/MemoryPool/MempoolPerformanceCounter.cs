using System;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolPerformanceCounter
	{
		public MempoolPerformanceCounter()
		{
			Start = DateTime.UtcNow;
		}

		public DateTime Start { get; }

		public TimeSpan Elapsed => DateTime.UtcNow - Start;

		private long mempoolSize;
		public long MempoolSize => mempoolSize;
		public void SetMempoolSize(long size)
		{
			Interlocked.Exchange(ref mempoolSize, size);
		}

		private long mempoolDynamicSize;
		public long MempoolDynamicSize => mempoolDynamicSize;
		public void SetMempoolDynamicSize(long size)
		{
			Interlocked.Exchange(ref mempoolDynamicSize, size);
		}

		private long mempoolOrphanSize;
		public long MempoolOrphanSize { get; set; }
		public void SetMempoolOrphanSize(long size)
		{
			Interlocked.Exchange(ref mempoolOrphanSize, size);
		}

		private long hitCount;
		public long HitCount => hitCount;
		public void AddHitCount(long count)
		{
			Interlocked.Add(ref hitCount, count);
		}

		public override string ToString()
		{
			var benchLogs = new StringBuilder();
			benchLogs.AppendLine(
				"MempoolSize: " + this.MempoolSize.ToString().PadRight(4) + 
				" DynamicSize: " + ((this.MempoolDynamicSize / 1000) + " kb").ToString().PadRight(6) +
				" OrphanSize: " + this.MempoolOrphanSize.ToString().PadRight(4));
			return benchLogs.ToString();
		}
	}

}
