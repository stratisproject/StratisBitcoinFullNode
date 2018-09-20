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

        /// <summary>
        /// Measures time to execute <c>OnPartialValidationSucceededAsync</c>.
        /// </summary>
        public IDisposable MeasureTotalConnectionTime()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.TotalConnectionTime.TotalExecutionsCount);
                Interlocked.Add(ref snapshot.TotalConnectionTime.TotalDelayTicks, elapsedTicks);
            });

            return stopwatch;
        }

        public IDisposable MeasureBlockConnectionFV()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.ConnectBlockFV.TotalExecutionsCount);
                Interlocked.Add(ref snapshot.ConnectBlockFV.TotalDelayTicks, elapsedTicks);
            });

            return stopwatch;
        }

        public IDisposable MeasureBlockConnectedSignal()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.BlockConnectedSignal.TotalExecutionsCount);
                Interlocked.Add(ref snapshot.BlockConnectedSignal.TotalDelayTicks, elapsedTicks);
            });

            return stopwatch;
        }

        public IDisposable MeasureBlockDisconnectedSignal()
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                ConsensusManagerPerformanceSnapshot snapshot = this.currentSnapshot;

                Interlocked.Increment(ref snapshot.BlockDisconnectedSignal.TotalExecutionsCount);
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
            return Math.Round((this.TotalDelayTicks / (double) this.TotalExecutionsCount) / 1000.0, 4);
        }
    }
}
