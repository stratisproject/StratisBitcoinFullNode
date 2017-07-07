using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    public interface IAsyncLoop
    {
        string Name { get; }

        Task Run(TimeSpan? repeatEvery = default(TimeSpan?), TimeSpan? startAfter = default(TimeSpan?));
        Task Run(CancellationToken cancellation, TimeSpan? repeatEvery = default(TimeSpan?), TimeSpan? startAfter = default(TimeSpan?));
    }

    public class AsyncLoop : IAsyncLoop
    {
        private readonly ILogger logger;
        readonly Func<CancellationToken, Task> loopAsync;

        public AsyncLoop(string name, ILogger logger, Func<CancellationToken, Task> loop)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            this.Name = name;
            this.logger = logger;
            this.loopAsync = loop;
        }

        public string Name { get; }

        public Task Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return this.Run(CancellationToken.None, repeatEvery, startAfter);
        }

        public Task Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));

            return this.StartAsync(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }

        public static Task Run(string name, ILogger logger, Func<CancellationToken, Task> loop, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            return new AsyncLoop(name, logger, loop).Run(repeatEvery, startAfter);
        }

        public static Task Run(string name, ILogger logger, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotNull(cancellation, nameof(cancellation));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));

            return new AsyncLoop(name, logger, loop).Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(1000), startAfter);
        }

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
						if(cancellation.IsCancellationRequested)
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
                    this.logger.LogCritical(new EventId(0), uncatchException, this.Name + " threw an unhandled exception");
                }
            }, cancellation);
        }

        /// <summary>
        /// Loop every so often until a condition is met, then execute the action and finish.
        /// </summary>       
        public static Task RunUntil(string name, ILogger logger, CancellationToken nodeCancellationToken, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan repeatEvery)
        {
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(nodeCancellationToken);
            return AsyncLoop.Run(name, logger, token =>
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