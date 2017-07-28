using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Periodic task executes an application defined task in a loop with a specified delay between two runs.</summary>
    public interface IPeriodicTask
    {
        /// <summary>Name of the application task. It is used for logging.</summary>
        string Name { get; }

        /// <summary>
        /// Executes the application task once.
        /// </summary>
        void RunOnce();

        /// <summary>
        /// Configures and starts the loop in which the application task is periodically executed.
        /// </summary>
        /// <param name="cancellation">Cancellation token that allows the caller to stop the loop.</param>
        /// <param name="refreshRate">Delay between two executions of the task.</param>
        /// <param name="delayStart">If true, the first execution of the task is made after the <paramref name="refreshRate"/> time.</param>
        /// <returns>This task to enable fluent code.</returns>
        PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false);
    }

    /// <inheritdoc />
    /// <remarks>This implementation spawns a new thread for each new periodic task.</remarks>
    public class PeriodicTask : IPeriodicTask
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        Action<CancellationToken> loop;

        /// <summary>Name of the application task. It is used for logging.</summary>
        private readonly string name;
        /// <inheritdoc />
        public string Name
        {
            get
            {
                return this.name;
            }
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="name">Name of the application task.</param>
        /// <param name="logger">Logger for this instance.</param>
        /// <param name="loop">Application defined task to execute periodically.</param>
        public PeriodicTask(string name, ILogger logger, Action<CancellationToken> loop)
        {
            this.name = name;
            this.logger = logger;
            this.loop = loop;
        }

        /// <inheritdoc />
        public PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false)
        {
            var t = new Thread(() =>
            {
                Exception uncatchException = null;
                this.logger.LogInformation(this.name + " starting");
                try
                {
                    if (delayStart)
                        cancellation.WaitHandle.WaitOne(refreshRate);

                    while (!cancellation.IsCancellationRequested)
                    {
                        this.loop(cancellation);
                        cancellation.WaitHandle.WaitOne(refreshRate);
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
                    this.logger.LogCritical(new EventId(0), uncatchException, this.name + " threw an unhandled exception");
                }
            });
            t.IsBackground = true;
            t.Name = this.name;
            t.Start();            
            return this;
        }

        /// <inheritdoc />
        public void RunOnce()
        {
            this.loop(CancellationToken.None);
        }
    }
}
