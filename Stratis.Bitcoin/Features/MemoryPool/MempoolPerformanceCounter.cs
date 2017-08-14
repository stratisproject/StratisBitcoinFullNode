using System;
using System.Text;
using System.Threading;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolPerformanceCounter
    {
        public MempoolPerformanceCounter()
        {
            this.Start = DateTime.UtcNow;
        }

        public DateTime Start { get; }

        public TimeSpan Elapsed => DateTime.UtcNow - this.Start;

        private long mempoolSize;
        public long MempoolSize => this.mempoolSize;
        public void SetMempoolSize(long size)
        {
            Interlocked.Exchange(ref this.mempoolSize, size);
        }

        private long mempoolDynamicSize;
        public long MempoolDynamicSize => this.mempoolDynamicSize;
        public void SetMempoolDynamicSize(long size)
        {
            Interlocked.Exchange(ref this.mempoolDynamicSize, size);
        }

        private long mempoolOrphanSize;
        public long MempoolOrphanSize { get; set; }
        public void SetMempoolOrphanSize(long size)
        {
            Interlocked.Exchange(ref this.mempoolOrphanSize, size);
        }

        private long hitCount;
        public long HitCount => this.hitCount;
        public void AddHitCount(long count)
        {
            Interlocked.Add(ref this.hitCount, count);
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
