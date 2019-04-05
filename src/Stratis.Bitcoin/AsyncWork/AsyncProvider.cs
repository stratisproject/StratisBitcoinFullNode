using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

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

        private (string Name, int Width)[] benchmarkColumnsDefinition = new[]
        {
            (Name: "Name", Width: 30),
            (Name: "Type", Width: 15),
            (Name: "Health", Width: 15),
            (Name: "Status", Width: 20)
        };

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
        public IAsyncLoop CreateAndRunAsyncLoopUntil(string name, CancellationToken cancellation, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null)
        {
            CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            return this.CreateAndRunAsyncLoop(name, token =>
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

        /// <inheritdoc />
        public IAsyncQueue<T> CreateAsyncQueue<T>()
        {
            return new AsyncQueue<T>();
        }

        /// <inheritdoc />

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

        /// <inheritdoc />

        public bool IsAsyncDelegateDequeuerRunning(string name)
        {
            lock (this.lockAsyncDelegates)
            {
                // task in the dictionaries are either running or faulted so we just look for an IAsyncDelegate with the given name and status not faulted.
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.IsDelegateWorker == true);
            }
        }

        /// <inheritdoc />

        public bool IsAsyncLoopRunning(string name)
        {
            lock (this.lockAsyncDelegates)
            {
                // task in the dictionaries are either running or faulted so we just look for a dequeuer with the given name and status not faulted.
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.IsLoop == true);
            }
        }

        /// <inheritdoc />
        [NoTrace]
        public void AddBenchStats(StringBuilder log)
        {
            List<AsyncTaskInfo> taskInformationsDump;
            lock (this.lockAsyncDelegates)
            {
                // takes a snapshot of the informations.
                taskInformationsDump = this.asyncDelegates.Values.ToList();
            }

            log.AppendLine("====== Async loops ======");
            log.AppendLine("Running".PadRight(20) + taskInformationsDump.Where(info => info.IsRunning).Count().ToString().PadRight(20));
            log.Append("Faulted".PadRight(20) + taskInformationsDump.Where(info => !info.IsRunning).Count().ToString().PadRight(20));
            log.AppendLine("------");

            var data =
                from info in taskInformationsDump
                orderby info.FriendlyName
                select new
                {
                    Columns = new string[] {
                        info.FriendlyName,
                        (info.IsLoop ? "Loop" : "Dequeuer"),
                        (info.IsRunning ? "Running" : "Faulted"),
                        info.Status.ToString()
                    },
                    Exception = info.Exception?.Message
                };

            foreach (var item in this.benchmarkColumnsDefinition)
            {
                log.Append(item.Name.PadRight(item.Width));
            }

            log.AppendLine();
            log.AppendLine("-".PadRight(this.benchmarkColumnsDefinition.Sum(column => column.Width), '-'));

            foreach (var row in data)
            {
                for (int iColumn = 0; iColumn < this.benchmarkColumnsDefinition.Length; iColumn++)
                {
                    log.Append(row.Columns[iColumn].PadRight(this.benchmarkColumnsDefinition[iColumn].Width));

                    // if exception != null means the loop is faulted, so I show the reason in a row under it, starting from the 2nd column (after the name)
                    if (row.Exception != null)
                    {
                        log.AppendLine();
                        log.Append(string.Empty.PadRight(this.benchmarkColumnsDefinition[0].Width));
                        log.Append($"* Fault Reason: {row.Exception}");
                    }
                }
            }

            log.AppendLine();
            log.AppendLine("-".PadRight(this.benchmarkColumnsDefinition.Sum(column => column.Width), '-'));
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
    }
}