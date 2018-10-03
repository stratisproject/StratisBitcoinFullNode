using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Allows running application defined in a loop with specific timing.
    /// </summary>
    public interface IAsyncLoop : IDisposable
    {
        /// <summary>Name of the loop. It is used for logging.</summary>
        string Name { get; }

        /// <summary>Interval between each execution of the task.</summary>
        TimeSpan RepeatEvery { get; set; }

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// The task representing the loop being executed.
        /// </summary>
        Task RunningTask { get; }
    }

    /// <summary>
    /// Allows running application defined in a loop with specific timing.
    /// <para>
    /// It is possible to specify a startup delay, which will cause the first execution of the task to be delayed.
    /// It is also possible to specify a delay between two executions of the task. And finally, it is possible
    /// to make the task run only once. Running the task for other than one or infinite number of times is not supported.
    /// </para>
    /// </summary>
    public class AsyncLoop : IAsyncLoop
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Application defined task that will be called and awaited in the async loop.
        /// The task is given a cancellation token that allows it to recognize that the caller wishes to cancel it.
        /// </summary>
        private readonly Func<CancellationToken, Task> loopAsync;

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public Task RunningTask { get; private set; }

        /// <inheritdoc />
        public TimeSpan RepeatEvery { get; set; }

        /// <summary>
        /// Initializes a named instance of the object.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="logger">Logger for the new instance.</param>
        /// <param name="loop">Application defined task that will be called and awaited in the async loop.</param>
        public AsyncLoop(string name, ILogger logger, Func<CancellationToken, Task> loop)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            this.Name = name;
            this.logger = logger;
            this.loopAsync = loop;
            this.RepeatEvery = TimeSpan.FromMilliseconds(1000);
        }

        /// <inheritdoc />
        public IAsyncLoop Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return this.Run(CancellationToken.None, repeatEvery, startAfter);
        }

        /// <inheritdoc />
        public IAsyncLoop Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));

            if (repeatEvery != null)
                this.RepeatEvery = repeatEvery.Value;

            this.RunningTask = this.StartAsync(cancellation, startAfter);

            return this;
        }

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="delayStart">Delay before the first run of the task, or null if no startup delay is required.</param>
        private Task StartAsync(CancellationToken cancellation, TimeSpan? delayStart = null)
        {
            return Task.Run(async () =>
            {
                Exception uncaughtException = null;
                try
                {
                    if (delayStart != null)
                    {
                        this.logger.LogInformation("{0} starting in {1} seconds.", this.Name, delayStart.Value.TotalSeconds);
                        await Task.Delay(delayStart.Value, cancellation).ConfigureAwait(false);
                    }

                    this.logger.LogInformation("{0} starting.", this.Name);

                    if (this.RepeatEvery == TimeSpans.RunOnce)
                    {
                        if (cancellation.IsCancellationRequested)
                            return;

                        await this.loopAsync(cancellation).ConfigureAwait(false);

                        return;
                    }

                    while (!cancellation.IsCancellationRequested)
                    {
                        await this.loopAsync(cancellation).ConfigureAwait(false);
                        if (!cancellation.IsCancellationRequested)
                            await Task.Delay(this.RepeatEvery, cancellation).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (!cancellation.IsCancellationRequested)
                        uncaughtException = ex;
                }
                catch (Exception ex)
                {
                    uncaughtException = ex;
                }
                finally
                {
                    this.logger.LogInformation(this.Name + " stopping.");
                }

                if (uncaughtException != null)
                {
                    // WARNING: Do NOT touch this line unless you want to fix weird AsyncLoop tests.
                    // The following line has to be called EXACTLY as it is.
                    this.logger.LogCritical(new EventId(0), uncaughtException, this.Name + " threw an unhandled exception");

                    // You can touch this one.
                    this.logger.LogError("{0} threw an unhandled exception: {1}", this.Name, uncaughtException.ToString());
                }
            }, cancellation);
        }

        /// <summary>
        /// Wait for the loop task to complete.
        /// </summary>
        public void Dispose()
        {
            if (!this.RunningTask.IsCanceled)
            {
                this.logger.LogInformation("Waiting for {0} to finish.", this.Name);
                this.RunningTask.Wait();
            }
        }
    }
}
