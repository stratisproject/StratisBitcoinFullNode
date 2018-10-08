using System;
using System.Diagnostics;
using TracerAttributes;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Replacement for the <see cref="System.Diagnostics.Stopwatch"/> class that allows the caller
    /// to use a convenient way of calling the watch with the <c>using</c> statement due to
    /// the implementation of <see cref="IDisposable"/> interface.
    /// </summary>
    /// <remarks>
    /// Note that we are using <see cref="DateTime.Ticks"/> as a basic unit of measurement,
    /// not <see cref="System.Diagnostics.Stopwatch.ElapsedTicks"/>.
    /// Issue that cover this subject is <see href="https://github.com/stratisproject/StratisBitcoinFullNode/issues/2391"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockFetchingTime(o)))
    /// {
    ///     // Time of anything executed here will be added to the used performance counter.
    /// }
    /// </code>
    /// </example>
    public class StopwatchDisposable : IDisposable
    {
        /// <summary>Stopwatch to measure elapsed ticks of the code block.</summary>
        private readonly Stopwatch watch;

        /// <summary>
        /// Action to execute when the measurement is done.
        /// <para>
        /// This is usually a performance counter.
        /// The argument of the action is the number of elapsed ticks of the code block.
        /// </para>
        /// </summary>
        private readonly Action<long> action;

        /// <summary>
        /// Creates a new disposable object and starts the time measurement.
        /// </summary>
        /// <param name="action">Action to execute when the measurement is done.</param>
        public StopwatchDisposable(Action<long> action)
        {
            Guard.NotNull(action, nameof(action));

            this.action = action;
            this.watch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Stops the time measurement and calls the action with the measured elapsed ticks.
        /// </summary>
        [NoTrace]
        public void Dispose()
        {
            this.watch.Stop();
            this.action(this.watch.Elapsed.Ticks);
        }
    }
}
