using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NBitcoin.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusManagerPerformanceCounter
    {
        /// <summary>Snapshot that is currently being populated.</summary>
        private ConsensusManagerPerformanceSnapshot currentSnapshot;

        public ConsensusManagerPerformanceCounter()
        {
            this.currentSnapshot = new ConsensusManagerPerformanceSnapshot();
        }

        public IDisposable MeasureBlockConnectedSignal()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.BlockConnectedSignal.ExecutedTimes);
                Interlocked.Add(ref snapshot.BlockConnectedSignal.TotalDelayTicks, elapsedTicks);
            });

            return stopwatch;
        }

        public IDisposable MeasureBlockDisconnectedSignal()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.BlockDisconnectedSignal.ExecutedTimes);
                Interlocked.Add(ref snapshot.BlockDisconnectedSignal.TotalDelayTicks, elapsedTicks);
            });

            return stopwatch;
        }

        /// <summary>Takes current snapshot.</summary>
        /// <remarks>Not thread-safe. Caller should ensure that it's not called from different threads at once.</remarks>
        public ConsensusManagerPerformanceSnapshot TakeSnapshot()
        {
            var newSnapshot = new ConsensusManagerPerformanceSnapshot();
            ConsensusManagerPerformanceSnapshot previousSnapshot = this.currentSnapshot;
            this.currentSnapshot = newSnapshot;

            return previousSnapshot;
        }
    }

    /// <summary>Snapshot of <see cref="ConsensusManager"/> performance.</summary>
    public class ConsensusManagerPerformanceSnapshot
    {
        public readonly ExecutionTimesAndDelay BlockDisconnectedSignal = new ExecutionTimesAndDelay();

        public readonly ExecutionTimesAndDelay BlockConnectedSignal = new ExecutionTimesAndDelay();

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine();
            builder.AppendLine("======ConsensusManager Bench======");

            builder.AppendLine($"Block connected signal: {this.BlockConnectedSignal.GetAvgExecutionTimeMs()} ms");
            builder.AppendLine($"Block disconnected signal: {this.BlockDisconnectedSignal.GetAvgExecutionTimeMs()} ms");

            return builder.ToString();
        }
    }

    public class ExecutionTimesAndDelay
    {
        public int ExecutedTimes;
        public long TotalDelayTicks;

        public ExecutionTimesAndDelay()
        {
            this.ExecutedTimes = 0;
            this.TotalDelayTicks = 0;
        }

        public double GetAvgExecutionTimeMs()
        {
            return Math.Round((this.TotalDelayTicks / (double) this.ExecutedTimes) / 1000.0, 4);
        }
    }
}
