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
    }
}