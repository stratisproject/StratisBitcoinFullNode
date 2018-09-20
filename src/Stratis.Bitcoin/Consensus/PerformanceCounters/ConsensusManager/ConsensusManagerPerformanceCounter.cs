using System;
using System.Threading;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.PerformanceCounters.ConsensusManager
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
}
