using System;
using System.Threading;

namespace NBitcoin
{
    public class PerformanceSnapshot
    {
        private readonly long totalWrittenBytes;
        public long TotalWrittenBytes { get { return this.totalWrittenBytes; } }

        private long totalReadBytes;
        public long TotalReadBytes { get { return this.totalReadBytes; } set { this.totalReadBytes = value; } }

        public TimeSpan Elapsed
        {
            get
            {
                return this.Taken - this.Start;
            }
        }

        public ulong ReadBytesPerSecond
        {
            get
            {
                return (ulong)((double)this.TotalReadBytes / this.Elapsed.TotalSeconds);
            }
        }

        public ulong WrittenBytesPerSecond
        {
            get
            {
                return (ulong)((double)this.TotalWrittenBytes / this.Elapsed.TotalSeconds);
            }
        }

        public PerformanceSnapshot(long readen, long written)
        {
            this.totalWrittenBytes = written;
            this.totalReadBytes = readen;
        }

        public static PerformanceSnapshot operator -(PerformanceSnapshot end, PerformanceSnapshot start)
        {
            if (end.Start != start.Start)
            {
                throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
            }

            if (end.Taken < start.Taken)
            {
                throw new InvalidOperationException("The difference of snapshot can't be negative");
            }

            return new PerformanceSnapshot(end.TotalReadBytes - start.TotalReadBytes, end.TotalWrittenBytes - start.TotalWrittenBytes)
            {
                Start = start.Taken,
                Taken = end.Taken
            };
        }

        public override string ToString()
        {
            return "Read : " + ToKBSec(this.ReadBytesPerSecond) + ", Write : " + ToKBSec(this.WrittenBytesPerSecond);
        }

        private string ToKBSec(ulong bytesPerSec)
        {
            double speed = ((double)bytesPerSec / 1024.0);
            return speed.ToString("0.00") + " KB/S)";
        }

        public DateTime Start { get; set; }

        public DateTime Taken { get; set; }
    }

    public class PerformanceCounter
    {
        private DateTime start;
        public DateTime Start { get { return this.start; } }
        public TimeSpan Elapsed
        {
            get
            {
                return DateTime.UtcNow - this.Start;
            }
        }

        private long writtenBytes;
        public long WrittenBytes { get { return this.writtenBytes; } }

        private long readBytes;
        public long ReadBytes { get { return this.readBytes; } }

        public PerformanceCounter()
        {
            this.start = DateTime.UtcNow;
        }

        public void AddWritten(long count)
        {
            Interlocked.Add(ref this.writtenBytes, count);
        }

        public void AddRead(long count)
        {
            Interlocked.Add(ref this.readBytes, count);
        }

        public PerformanceSnapshot Snapshot()
        {
            var snap = new PerformanceSnapshot(this.ReadBytes, this.WrittenBytes)
            {
                Start = this.Start,
                Taken = DateTime.UtcNow
            };
            return snap;
        }

        public override string ToString()
        {
            return Snapshot().ToString();
        }

        public void Add(PerformanceCounter counter)
        {
            AddWritten(counter.WrittenBytes);
            AddRead(counter.ReadBytes);
        }
    }
}