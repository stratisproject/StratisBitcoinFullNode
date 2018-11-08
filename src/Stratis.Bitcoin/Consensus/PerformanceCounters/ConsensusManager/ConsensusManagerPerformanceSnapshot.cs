using System;
using System.Text;
using System.Threading;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus.PerformanceCounters.ConsensusManager
{
    /// <summary>Snapshot of <see cref="ConsensusManager"/> performance.</summary>
    public class ConsensusManagerPerformanceSnapshot
    {
        public ExecutionsCountAndDelay TotalConnectionTime { get; }

        public ExecutionsCountAndDelay ConnectBlockFV { get; }

        public ExecutionsCountAndDelay BlockDisconnectedSignal { get; }

        public ExecutionsCountAndDelay BlockConnectedSignal { get; }

        public ConsensusManagerPerformanceSnapshot()
        {
            this.TotalConnectionTime = new ExecutionsCountAndDelay();
            this.ConnectBlockFV = new ExecutionsCountAndDelay();
            this.BlockDisconnectedSignal = new ExecutionsCountAndDelay();
            this.BlockConnectedSignal = new ExecutionsCountAndDelay();
        }

        [NoTrace]
        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine();
            builder.AppendLine("======ConsensusManager Bench======");

            builder.AppendLine($"Total connection time (FV, CHT upd, Rewind, Signaling): {this.TotalConnectionTime.GetAvgExecutionTimeMs()} ms");

            builder.AppendLine($"Block connection (FV excluding rewind): {this.ConnectBlockFV.GetAvgExecutionTimeMs()} ms");

            builder.AppendLine($"Block connected signal: {this.BlockConnectedSignal.GetAvgExecutionTimeMs()} ms");
            builder.AppendLine($"Block disconnected signal: {this.BlockDisconnectedSignal.GetAvgExecutionTimeMs()} ms");

            return builder.ToString();
        }
    }

    public class ExecutionsCountAndDelay
    {
        private int totalExecutionsCount;
        private long totalDelayTicks;

        public ExecutionsCountAndDelay()
        {
            this.totalExecutionsCount = 0;
            this.totalDelayTicks = 0;
        }

        public double GetAvgExecutionTimeMs()
        {
            return Math.Round(TimeSpan.FromTicks(this.totalDelayTicks).TotalMilliseconds / this.totalExecutionsCount, 4);
        }

        public void Increment(long elapsedTicks)
        {
            Interlocked.Increment(ref this.totalExecutionsCount);
            Interlocked.Add(ref this.totalDelayTicks, elapsedTicks);
        }
    }
}