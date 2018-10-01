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
            return new StopwatchDisposable((elapsed) => this.currentSnapshot.TotalConnectionTime.Increment(elapsed));
        }

        public IDisposable MeasureBlockConnectionFV()
        {
            return new StopwatchDisposable((elapsed) => this.currentSnapshot.ConnectBlockFV.Increment(elapsed));
        }

        public IDisposable MeasureBlockConnectedSignal()
        {
            return new StopwatchDisposable((elapsed) => this.currentSnapshot.BlockConnectedSignal.Increment(elapsed));
        }

        public IDisposable MeasureBlockDisconnectedSignal()
        {
            return new StopwatchDisposable((elapsed) => this.currentSnapshot.BlockDisconnectedSignal.Increment(elapsed));
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