using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.AsyncWork
{
    /// <summary>
    /// Provides functionality for creating and tracking asynchronous operations that happens in background.
    /// </summary>
    public partial class AsyncProvider : IAsyncProvider
    {
        private const int DefaultLoopRepeatInterval = 1000;

        private object lockAsyncDelegates;

        /// <summary>
        /// Holds a list of currently running async delegates or delegates that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncDelegates"/> lock
        /// </summary>
        private Dictionary<IAsyncDelegate, AsyncTaskInfo> asyncDelegates;

        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private ISignals signals;
        private readonly INodeLifetime nodeLifetime;

        public AsyncProvider(ILoggerFactory loggerFactory, ISignals signals, INodeLifetime nodeLifetime)
        {
            this.lockAsyncDelegates = new object();

            this.asyncDelegates = new Dictionary<IAsyncDelegate, AsyncTaskInfo>();

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

                this.asyncDelegates.Add(newDelegate, new AsyncTaskInfo(friendlyName, true));
            }

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateUnhandledException, newDelegate, TaskContinuationOptions.OnlyOnFaulted);

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            newDelegate.ConsumerTask.ContinueWith(this.onAsyncDelegateCompleted, newDelegate, TaskContinuationOptions.NotOnFaulted);

            return newDelegate;
        }

        /// <summary>
        ///  This method is called when a Task running an <see cref="IAsyncDelegate"/> captured an unhandled exception.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegate"/> that's run by the delegateTask</param>
        /// <remarks>state can be either of type <see cref="IAsyncDelegateDequeuer{T}"/> or <see cref="IAsyncLoop"/></remarks>
        private void onAsyncDelegateUnhandledException(Task task, object state)
        {
            AsyncTaskInfo delegateInfo;
            lock (this.lockAsyncDelegates)
            {
                if (this.asyncDelegates.TryGetValue((IAsyncDelegate)state, out delegateInfo))
                {
                    // casted to IAsyncTaskInfoSetter to be able to set properties
                    IAsyncTaskInfoSetter infoSetter = delegateInfo;

                    infoSetter.Exception = task.Exception.GetBaseException();
                    infoSetter.Status = task.Status;
                }
                else
                {
                    // Should never happen.
                    this.logger.LogError("Cannot find the AsyncDelegateInfo related to the faulted task with Id {0}.", task.Id);
                    return;
                }

                this.logger.LogError(task.Exception.GetBaseException(), "Unhandled exception for async delegate worker {0}.", delegateInfo.FriendlyName);
            }
        }

        /// <summary>
        ///  This method is called when a Task running an <see cref="IAsyncDelegate"/> completed or was canceled.
        ///  It removes the task information from the internal dictionary.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegate"/> that's run by the delegateTask</param>
        private void onAsyncDelegateCompleted(Task task, object state)
        {
            bool removed;
            lock (this.lockAsyncDelegates)
            {
                removed = this.asyncDelegates.Remove((IAsyncDelegate)state);
            }

            if (removed)
            {
                this.logger.LogTrace("IAsyncDelegate task Removed. Id: {0}.", task.Id);
            }
            else
            {
                // Should never happen.
                this.logger.LogError("Cannot find the IAsyncDelegate task with Id {0}.", task.Id);
            }
        }

        /// <inheritdoc />
        public IAsyncLoop CreateAndRunAsyncLoop(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(loop, nameof(loop));
            Guard.NotNull(cancellation, nameof(cancellation));

            // instantiate the loop
            IAsyncLoop loopInstance = new AsyncLoop(name, this.logger, loop);

            Task loopTask;
            lock (this.asyncDelegates)
            {
                this.asyncDelegates.Add(loopInstance, new AsyncTaskInfo(name, false));
            }

            loopTask = loopInstance.Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(DefaultLoopRepeatInterval), startAfter).RunningTask;

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            loopTask.ContinueWith(this.onAsyncDelegateUnhandledException, loopInstance, TaskContinuationOptions.OnlyOnFaulted);

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            loopTask.ContinueWith(this.onAsyncDelegateCompleted, loopInstance, TaskContinuationOptions.NotOnFaulted);

            return loopInstance;
        }

        /// <inheritdoc />
        public IAsyncQueue<T> CreateAsyncQueue<T>()
        {
            return new AsyncQueue<T>();
        }

        /// <summary>
        /// Determines whether a specified <see cref="IAsyncDelegate" /> is running.
        /// </summary>
        /// <param name="asyncDelegate">The asynchronous delegate to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IAsyncDelegate" /> is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when loops are transient and does not act like singletons.
        /// This method is mostly used for tests.
        /// state can be either of type <see cref="IAsyncDelegateDequeuer{T}" /> or <see cref="IAsyncLoop" />
        /// </remarks>
        public bool IsAsyncDelegateDequeuerRunning(IAsyncDelegate asyncDelegate)
        {
            lock (this.lockAsyncDelegates)
            {
                if (this.asyncDelegates.TryGetValue(asyncDelegate, out AsyncTaskInfo delegateInfo))
                {
                    // task in the dictionaries are either running or faulted so we just look for an asyncDelegate with a not faulted status.
                    return delegateInfo.Status != TaskStatus.Faulted;
                }

                return false;
            }
        }

        /// <summary>
        /// Determines whether an <see cref="IAsyncDelegateDequeuer{T}" /> with the specified name is running.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        ///   <c>true</c> if an <see cref="IAsyncDelegateDequeuer{T}" /> with the specified name is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when <see cref="IAsyncDelegateDequeuer{T}" /> are transient and does not act like singletons. This method is mostly used for tests.
        /// </remarks>
        public bool IsAsyncDelegateDequeuerRunning(string name)
        {
            lock (this.lockAsyncDelegates)
            {
                // task in the dictionaries are either running or faulted so we just look for an IAsyncDelegate with the given name and status not faulted.
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.IsDelegateWorker == true);
            }
        }

        /// <summary>
        /// Determines whether an <see cref="IAsyncLoop" /> with the specified name is running.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        ///   <c>true</c> if an <see cref="IAsyncLoop" /> with the specified name is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when <see cref="IAsyncLoop" /> are transient and does not act like singletons. This method is mostly used for tests.
        /// </remarks>
        public bool IsAsyncLoopRunning(string name)
        {
            lock (this.lockAsyncDelegates)
            {
                // task in the dictionaries are either running or faulted so we just look for a dequeuer with the given name and status not faulted.
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.IsLoop == true);
            }
        }
    }
}