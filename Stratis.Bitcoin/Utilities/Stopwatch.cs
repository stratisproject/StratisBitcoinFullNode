using System;

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
    /// <para>
    /// WARNING: This class is not thread-safe. You need a separate instance for each paralel 
    /// execution flow.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Assuming "this.watch" is an instance of "Stopwatch".
    /// using (this.watch.Start(o => this.Validator.PerformanceCounter.AddBlockFetchingTime(o)))
    /// {
    ///     // Time of anything executed here will be added to the used performance counter.
    /// }
    /// </code>
    /// </example>
    public class Stopwatch
    {
        /// <summary>
        /// Helper class that allows the convenient <c>using</c> calls.
        /// </summary>
        private class StopwatchDisposable : IDisposable
        {
            /// <summary>Stopwatch to measure elapsed ticks of the code block.</summary>
            private readonly System.Diagnostics.Stopwatch watch;

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
            /// <param name="watch">Stopwatch to measure elapsed ticks of the code block.</param>
            /// <param name="action">Action to execute when the measurement is done.</param>
            public StopwatchDisposable(System.Diagnostics.Stopwatch watch, Action<long> action)
            {
                this.watch = watch;
                this.action = action;
                watch.Restart();
            }

            /// <summary>
            /// Stops the time measurement and calls the action with the measured elapsed ticks.
            /// </summary>
            public void Dispose()
            {
                this.watch.Stop();
                this.action(this.watch.Elapsed.Ticks);
            }
        }

        /// <summary>Stopwatch to be used by <see cref="StopwatchDisposable"/> instances.</summary>
        private readonly System.Diagnostics.Stopwatch watch;

        /// <summary>Initializes a new instance of the object.</summary>
        public Stopwatch()
        {
            this.watch = new System.Diagnostics.Stopwatch();
        }

        /// <summary>
        /// Starts the time measurement.
        /// </summary>
        /// <param name="action">Action to execute when the measurement is done. See <see cref="StopwatchDisposable.action"/>.</param>
        /// <returns>Disposable interface for convenient calling with <c>using</c> keyword.</returns>
        public IDisposable Start(Action<long> action)
        {
            return new StopwatchDisposable(this.watch, action);
        }
    }
}
