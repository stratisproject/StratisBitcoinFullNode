﻿using System;
using System.Collections.Generic;
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
        private const int DefaultLoopRepeatInterval = 1000;

        private object lockAsyncDelegates;
        private object lockAsyncLoops;

        /// <summary>
        /// Holds a list of currently running async delegates or delegates that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncDelegates"/> lock
        /// </summary>
        private Dictionary<IAsyncDelegateWorker, AsyncTaskInfo> asyncDelegateWorkers;

        /// <summary>
        /// Holds a list of currently running async loops or loops that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncLoops"/> lock
        /// </summary>
        private Dictionary<IAsyncLoop, AsyncTaskInfo> asyncLoops;

        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private ISignals signals;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncLoopFactory asyncLoopFactory;

        public BackgroundWorkProvider(ILoggerFactory loggerFactory, ISignals signals, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
        {
            this.lockAsyncDelegates = new object();
            this.lockAsyncLoops = new object();

            this.asyncDelegateWorkers = new Dictionary<IAsyncDelegateWorker, AsyncTaskInfo>();

            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(nameof(BackgroundWorkProvider));

            this.signals = Guard.NotNull(signals, nameof(signals));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.asyncLoopFactory = Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
        }

        /// <inheritdoc />
        public IAsyncDelegateWorker CreateAndRunAsyncDelegate<T>(string friendlyName, Func<T, CancellationToken, Task> @delegate)
        {
            AsyncQueue<T> newDelegate;

            lock (this.lockAsyncDelegates)
            {
                newDelegate = new AsyncQueue<T>(new AsyncQueue<T>.OnEnqueueAsync(@delegate));

                // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
                newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateUnhandledException, newDelegate, TaskContinuationOptions.OnlyOnFaulted);

                // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
                newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateCompleted, newDelegate, TaskContinuationOptions.NotOnFaulted);

                this.asyncDelegateWorkers.Add(newDelegate, new AsyncTaskInfo(friendlyName));
            }

            return newDelegate;
        }

        /// <summary>
        ///  This method is called when delegateTask had an unhandled exception.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegateWorker"/> that's run by the delegateTask</param>
        private void onAsyncDelegateUnhandledException(Task task, object state)
        {
            var key = (IAsyncDelegateWorker)state;

            AsyncTaskInfo delegateInfo;
            lock (this.lockAsyncDelegates)
            {
                if (this.asyncDelegateWorkers.TryGetValue(key, out delegateInfo))
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
            IAsyncDelegateWorker key = (IAsyncDelegateWorker)state;

            bool removed;
            lock (this.lockAsyncDelegates)
            {
                removed = this.asyncDelegateWorkers.Remove(key);
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
                loopTask = loopInstance.Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(DefaultLoopRepeatInterval), startAfter).RunningTask;

                this.asyncLoops.Add(loopInstance, new AsyncTaskInfo(name));
            }

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
        /// <param name="state">The <see cref="IAsyncDelegateWorker"/> that's run by the delegateTask</param>
        private void onAsyncLoopUnhandledException(Task task, object state)
        {
            IAsyncDelegateWorker key = (IAsyncDelegateWorker)state;

            AsyncTaskInfo delegateInfo;
            lock (this.lockAsyncLoops)
            {
                if (this.asyncDelegateWorkers.TryGetValue(key, out delegateInfo))
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
            IAsyncDelegateWorker key = (IAsyncDelegateWorker)state;

            bool removed;
            lock (this.lockAsyncLoops)
            {
                removed = this.asyncDelegateWorkers.Remove(key);
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
        public IAsyncQueue<T> CreateAsyncQueue<T>()
        {
            return new AsyncQueue<T>();
        }
    }
}
