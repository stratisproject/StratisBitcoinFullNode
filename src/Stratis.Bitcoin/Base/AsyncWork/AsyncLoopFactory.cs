using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Factory for creating and also possibly starting application defined tasks inside async loop.</summary>
    public interface IAsyncLoopFactory
    {
        /// <summary>
        /// Creates a new async loop.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="loop">Application defined task that will be called and awaited in the async loop.</param>
        /// <returns>Newly created async loop that can be started on demand.</returns>
        IAsyncLoop Create(string name, Func<CancellationToken, Task> loop);

        /// <summary>
        /// Starts an application defined task inside a newly created async loop.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="loop">Application defined task that will be called and awaited in the async loop.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Starts an application defined task inside a newly created async loop.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="loop">Application defined task that will be called and awaited in the async loop.</param>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Waits until a condition is met, then executes the action and completes.
        /// <para>
        /// Waiting is implemented using the async loop for which the task is defined as execution of the condition method
        /// followed by execution of the action if the condition is satisfied - i.e. returns true. If the condition is
        /// not satisfied, the loop waits as per <see cref="repeatEvery"/> setting and then it can be attempted again.
        /// </para>
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="nodeCancellationToken">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="condition">Condition to be tested.</param>
        /// <param name="action">Method to execute once the condition is met.</param>
        /// <param name="onException">Method to execute if an exception occurs during evaluation of the condition or during execution of the <see cref="action"/>.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <returns></returns>
        IAsyncLoop RunUntil(string name, CancellationToken nodeCancellationToken, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan repeatEvery);
    }

    /// <inheritdoc />
    public sealed class AsyncLoopFactory : IAsyncLoopFactory
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory to create logger for the object and for the async loops it creates.</param>
        /// <remarks>TODO: It might be a better idea to pass factory to the newly created loops so that new loggers can be created for each loop.</remarks>
        public AsyncLoopFactory(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(typeof(FullNode).FullName);
        }

        /// <inheritdoc />
        public IAsyncLoop Create(string name, Func<CancellationToken, Task> loop)
        {
            return new AsyncLoop(name, this.logger, loop);
        }

        /// <inheritdoc />
        public IAsyncLoop Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return new AsyncLoop(name, this.logger, loop).Run(repeatEvery, startAfter);
        }

        /// <inheritdoc />
        public IAsyncLoop Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            return new AsyncLoop(name, this.logger, loop).Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }

        /// <inheritdoc />
        public IAsyncLoop RunUntil(string name, CancellationToken nodeCancellationToken, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan repeatEvery)
        {
            CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(nodeCancellationToken);
            return this.Run(name, token =>
            {
                try
                {
                    // loop until the condition is met, then execute the action and finish.
                    if (condition())
                    {
                        action();

                        linkedTokenSource.Cancel();
                    }
                }
                catch (Exception e)
                {
                    onException(e);
                    linkedTokenSource.Cancel();
                }

                return Task.CompletedTask;
            },
            linkedTokenSource.Token,
            repeatEvery: repeatEvery);
        }
    }
}
