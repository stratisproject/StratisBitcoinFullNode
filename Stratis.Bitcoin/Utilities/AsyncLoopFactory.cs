using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    public interface IAsyncLoopFactory
    {
        IAsyncLoop Create(string name, Func<CancellationToken, Task> loop);

        Task Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);
        Task Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        Task RunUntil(string name, CancellationToken nodeCancellationToken, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan repeatEvery);
    }

    public class AsyncLoopFactory : IAsyncLoopFactory
    {
        private readonly ILogger logger;

        public AsyncLoopFactory(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(typeof(FullNode).FullName);
        }

        public IAsyncLoop Create(string name, Func<CancellationToken, Task> loop)
        {
            return new AsyncLoop(name, this.logger, loop);
        }

        public Task Run(string name, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return new AsyncLoop(name, this.logger, loop).Run(repeatEvery, startAfter);
        }

        public Task Run(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            return new AsyncLoop(name, this.logger, loop).Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }

        /// <summary>
        /// Loop every so often until a condition is met, then execute the action and finish.
        /// </summary>       
        public Task RunUntil(string name, CancellationToken nodeCancellationToken, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan repeatEvery)
        {
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(nodeCancellationToken);
            return Run(name, token =>
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