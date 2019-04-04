using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.AsyncWork
{
    /// <summary>
    /// Provides functionality for creating and tracking asynchronous operations that happens in background.
    /// </summary>
    public partial class AsyncProvider : IAsyncProvider
    {
        private const int DefaultLoopRepeatInterval = 1000;

        private object lockAsyncDelegates;
        private object lockAsyncLoops;

        /// <summary>
        /// Holds a list of currently running async delegates or delegates that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncDelegates"/> lock
        /// </summary>
        private Dictionary<IAsyncDelegateDequeuer, AsyncTaskInfo> asyncDelegateWorkers;

        /// <summary>
        /// Holds a list of currently running async loops or loops that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncLoops"/> lock
        /// </summary>
        private Dictionary<IAsyncLoop, AsyncTaskInfo> asyncLoops;

        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private ISignals signals;
        private readonly INodeLifetime nodeLifetime;

        public AsyncProvider(ILoggerFactory loggerFactory, ISignals signals, INodeLifetime nodeLifetime)
        {
            this.lockAsyncDelegates = new object();
            this.lockAsyncLoops = new object();

            this.asyncDelegateWorkers = new Dictionary<IAsyncDelegateDequeuer, AsyncTaskInfo>();

            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(nameof(AsyncProvider));

            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
        }

        /// <inheritdoc />
        public IAsyncDelegateDequeuer<T> CreateAndRunAsyncDelegateDequeuer<T>(string friendlyName, Func<T, CancellationToken, Task> @delegate)
        {
            AsyncQueue<T> newDelegate;

            lock (this.lockAsyncDelegates)
            {
                newDelegate = new AsyncQueue<T>(new AsyncQueue<T>.OnEnqueueAsync(@delegate));

                this.asyncDelegateWorkers.Add(newDelegate, new AsyncTaskInfo(friendlyName));
            }

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateUnhandledException, newDelegate, TaskContinuationOptions.OnlyOnFaulted);

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateCompleted, newDelegate, TaskContinuationOptions.NotOnFaulted);

            return newDelegate;
        }

        /// <summary>
        ///  This method is called when delegateTask had an unhandled exception.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegateDequeuer"/> that's run by the delegateTask</param>
        private void onAsyncDelegateUnhandledException(Task task, object state)
        {
            AsyncTaskInfo delegateInfo;
            lock (this.lockAsyncDelegates)
            {
                if (this.asyncDelegateWorkers.TryGetValue((IAsyncDelegateDequeuer)state, out delegateInfo))
                {
                    IAsyncTaskInfoSetter infoSetter;
                    infoSetter = (IAsyncTaskInfoSetter)delegateInfo;
                    infoSetter.Exception = task.Exception.GetBaseException();
                    infoSetter.Status = task.Status;
                }
                else
                {
                    // Should never happen.
                    this.logger.LogError("Cannot find the AsyncDelegateInfo related to the faulted task with Id {0}", task.Id);
                    return;
                }

                this.logger.LogError(task.Exception.GetBaseException(), "Unhandled exception for async delegate worker {0}", delegateInfo.FriendlyName);
            }
        }

        /// <summary>
        ///  This method is called when delegateTask completed or was canceled.
        ///  It removes the delegate worker from the dictionary.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The IAsyncDelegate that's run by the delegateTask</param>
        private void onAsyncDelegateCompleted(Task task, object state)
        {
            bool removed;
            lock (this.lockAsyncDelegates)
            {
                removed = this.asyncDelegateWorkers.Remove((IAsyncDelegateDequeuer)state);
            }

            if (removed)
            {
                this.logger.LogTrace("Async delegate worker task removed. Id: {0}", task.Id);
            }
            else
            {
                // Should never happen.
                this.logger.LogError("Cannot find the async delegate worker task with Id {0}", task.Id);
            }
        }

        /// <inheritdoc />
        public IAsyncLoop CreateAndRunAsyncLoop<T>(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));
            Guard.NotNull(cancellation, nameof(cancellation));

            // instantiate the loop
            IAsyncLoop loopInstance = new AsyncLoop(name, this.logger, loop);

            Task loopTask;
            lock (this.lockAsyncLoops)
            {
                this.asyncLoops.Add(loopInstance, new AsyncTaskInfo(name));
            }

            loopTask = loopInstance.Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(DefaultLoopRepeatInterval), startAfter).RunningTask;

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            loopTask.ContinueWith(this.onAsyncLoopUnhandledException, loopInstance, TaskContinuationOptions.OnlyOnFaulted);

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            loopTask.ContinueWith(this.onAsyncLoopCompleted, loopInstance, TaskContinuationOptions.NotOnFaulted);

            return loopInstance;
        }

        /// <summary>
        ///  This method is called when delegateTask had an unhandled exception.
        /// </summary>
        /// <param name="task">The loop task.</param>
        /// <param name="state">The <see cref="IAsyncDelegateDequeuer"/> that's run by the delegateTask</param>
        private void onAsyncLoopUnhandledException(Task task, object state)
        {
            AsyncTaskInfo delegateInfo;
            lock (this.lockAsyncLoops)
            {
                if (this.asyncLoops.TryGetValue((IAsyncLoop)state, out delegateInfo))
                {
                    IAsyncTaskInfoSetter infoSetter;
                    infoSetter = (IAsyncTaskInfoSetter)delegateInfo;
                    infoSetter.Exception = task.Exception.GetBaseException();
                    infoSetter.Status = task.Status;
                }
                else
                {
                    // Should never happen.
                    this.logger.LogError("Cannot find the Async Loop related to the faulted task with Id {0}", task.Id);
                    return;
                }

                this.logger.LogError(task.Exception.GetBaseException(), "Unhandled exception for async loop {0} with task Id {1}", delegateInfo.FriendlyName, task.Id);
            }
        }

        /// <summary>
        ///  This method is called when delegateTask completed or was canceled.
        ///  It removes the loop from the dictionary.
        /// </summary>
        /// <param name="task">The loop task.</param>
        /// <param name="state">The IAsyncDelegate that's run by the delegateTask</param>
        private void onAsyncLoopCompleted(Task task, object state)
        {
            bool removed;
            lock (this.lockAsyncLoops)
            {
                removed = this.asyncLoops.Remove((IAsyncLoop)state);
            }

            if (removed)
            {
                this.logger.LogTrace("Async loop task removed. Id: {0}", task.Id);
            }
            else
            {
                // Should never happen.
                this.logger.LogError("Cannot find the async loop task with Id {0}", task.Id);
            }
        }

        /// <inheritdoc />
        public IAsyncQueue<T> CreateAsyncQueue<T>()
        {
            return new AsyncQueue<T>();
        }
    }
}
