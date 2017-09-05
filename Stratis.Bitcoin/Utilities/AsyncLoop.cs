using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Allows running application defined in a loop with specific timing.
    /// </summary>
    public interface IAsyncLoop
    {
        /// <summary>Name of the loop. It is used for logging.</summary>
        string Name { get; }

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="repeatEvery">Interval between each execution of the task. 
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop. 
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        Task Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="repeatEvery">Interval between each execution of the task. 
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop. 
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        Task Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);
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
        readonly Func<CancellationToken, Task> loopAsync;

        /// <inheritdoc />
        public string Name { get; }

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
        }

        /// <inheritdoc />
        public Task Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return this.Run(CancellationToken.None, repeatEvery, startAfter);
        }

        /// <inheritdoc />
        public Task Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));

            return this.StartAsync(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="refreshRate">Interval between each execution of the task. 
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop. 
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="delayStart">Delay before the first run of the task, or null if no startup delay is required.</param>
        private Task StartAsync(CancellationToken cancellation, TimeSpan refreshRate, TimeSpan? delayStart = null)
        {
            return Task.Run(async () =>
            {
                Exception uncatchException = null;
                this.logger.LogInformation(this.Name + " starting");
                try
                {
                    if (delayStart != null)
                        await Task.Delay(delayStart.Value, cancellation).ConfigureAwait(false);

                    if (refreshRate == TimeSpans.RunOnce)
                    {
                        if (cancellation.IsCancellationRequested)
                            return;

                        await this.loopAsync(cancellation).ConfigureAwait(false);

                        return;
                    }

                    while (!cancellation.IsCancellationRequested)
                    {
                        await this.loopAsync(cancellation).ConfigureAwait(false);
                        await Task.Delay(refreshRate, cancellation).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (!cancellation.IsCancellationRequested)
                        uncatchException = ex;
                }
                catch (Exception ex)
                {
                    uncatchException = ex;
                }
                finally
                {
                    this.logger.LogInformation(this.Name + " stopping");
                }

                if (uncatchException != null)
                {
                    // WARNING: Do NOT touch this line unless you want to fix weird AsyncLoop tests.
                    // The following line has to be called EXACTLY as it is.
                    this.logger.LogCritical(new EventId(0), uncatchException, this.Name + " threw an unhandled exception");

                    // You can touch this one.
                    this.logger.LogDebug("{0} threw an unhandled exception: {1}", this.Name, uncatchException.ToString());
                }
            }, cancellation);
        }
    }
}