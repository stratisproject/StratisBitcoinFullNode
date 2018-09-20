using System;
using System.Text;

namespace Stratis.Bitcoin.Consensus.PerformanceCounters.ConsensusManager
{

    /// <summary>Snapshot of <see cref="ConsensusManager"/> performance.</summary>
    public class ConsensusManagerPerformanceSnapshot
    {
        public readonly ExecutionsCountAndDelay TotalConnectionTime = new ExecutionsCountAndDelay();

        public readonly ExecutionsCountAndDelay ConnectBlockFV = new ExecutionsCountAndDelay();

        public readonly ExecutionsCountAndDelay BlockDisconnectedSignal = new ExecutionsCountAndDelay();

        public readonly ExecutionsCountAndDelay BlockConnectedSignal = new ExecutionsCountAndDelay();

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
        public int TotalExecutionsCount;
        public long TotalDelayTicks;

        public ExecutionsCountAndDelay()
        {
            this.TotalExecutionsCount = 0;
            this.TotalDelayTicks = 0;
        }

        public double GetAvgExecutionTimeMs()
        {
            return Math.Round((this.TotalDelayTicks / (double)this.TotalExecutionsCount) / 1000.0, 4);
        }
    }
}
