using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.BackgroundWork
{
    /// <summary>
    /// Provides functionality for creating and tracking operations that happens in background.
    /// </summary>
    public partial class BackgroundWorkProvider : IBackgroundWorkProvider
    {
        private object lockObject;

        /// <summary>
        /// Holds a list of currently running async delegates or delegates that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockObject"/> lock
        /// </summary>
        private Dictionary<IAsyncDelegateWorker, AsyncDelegateInfo> asyncDelegates;

        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private ISignals signals;
        private readonly INodeLifetime nodeLifetime;

        public BackgroundWorkProvider(ILoggerFactory loggerFactory, ISignals signals, INodeLifetime nodeLifetime)
        {
            this.lockObject = new object();
            this.asyncDelegates = new Dictionary<IAsyncDelegateWorker, AsyncDelegateInfo>();

            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(nameof(BackgroundWorkProvider));

            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
        }

        public IAsyncDelegateWorker CreateAndRunAsyncDelegate<T>(string friendlyName, Func<T, CancellationToken, Task> @delegate)
        {
            AsyncQueue<T> newDelegate;

            lock (this.lockObject)
            {
                newDelegate = new AsyncQueue<T>(new AsyncQueue<T>.OnEnqueueAsync(@delegate));

                // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
                newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateUnhandledException, newDelegate, TaskContinuationOptions.OnlyOnFaulted);

                // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
                newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateCompleted, newDelegate, TaskContinuationOptions.NotOnFaulted);

                this.asyncDelegates.Add(newDelegate, new AsyncDelegateInfo(friendlyName));
            }

            return newDelegate;
        }

        /// <summary>
        ///  This method is called when delegateTask had an unhandled exception.
        /// </summary>
        /// <param name="delegateTask">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegateWorker"/> that's run by the delegateTask</param>
        private void onAsyncDelegateUnhandledException(Task delegateTask, object state)
        {
            IAsyncDelegateWorker key = (IAsyncDelegateWorker)state;

            lock (this.lockObject)
            {
                if (this.asyncDelegates.TryGetValue(key, out AsyncDelegateInfo value))
                {
                    IAsyncDelegateInfoSetter info = (IAsyncDelegateInfoSetter)value;
                    info.Exception = delegateTask.Exception.GetBaseException();
                    info.Status = delegateTask.Status;
                }
            }

            this.logger.LogError(delegateTask.Exception.GetBaseException(), "An unhandled exception");
        }

        /// <summary>
        ///  This method is called when delegateTask completed or was canceled.
        /// </summary>
        /// <param name="delegateTask">The delegate task.</param>
        /// <param name="state">The IAsyncDelegate that's run by the delegateTask</param>
        private void onAsyncDelegateCompleted(Task delegateTask, object state) => throw new NotImplementedException();

        public IAsyncLoop CreateAsyncLoop<T>(string loopName, Func<CancellationToken, Task> loop)
        {
            throw new NotImplementedException();
        }

        public IAsyncQueue<T> CreateAsyncQueue<T>()
        {
            throw new NotImplementedException();
        }
    }
}
