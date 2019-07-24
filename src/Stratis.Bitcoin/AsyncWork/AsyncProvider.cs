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
    /// Provides functionality for creating and tracking asynchronous operations that happen in the background.
    /// </summary>
    public partial class AsyncProvider : IAsyncProvider
    {
        private const int DefaultLoopRepeatInterval = 1000;

        private object lockAsyncDelegates;
        private object lockRegisteredTasks;

        /// <summary>
        /// Holds a list of currently running async delegates or delegates that stopped because of unhandled exceptions.
        /// Protected by <see cref="lockAsyncDelegates"/> lock
        /// </summary>
        private Dictionary<IAsyncDelegate, AsyncTaskInfo> asyncDelegates;

        /// <summary>
        /// Holds a list of currently registered tasks with their health status.
        /// Protected by <see cref="lockRegisteredTasks"/> lock
        /// </summary>
        private Dictionary<Task, AsyncTaskInfo> registeredTasks;

        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private ISignals signals;
        private readonly INodeLifetime nodeLifetime;

        private (string Name, int Width)[] benchmarkColumnsDefinition = new[]
        {
            (Name: "Name", Width: 80),
            (Name: "Type", Width: 15),
            (Name: "Health", Width: 15)
        };

        /// <inheritdoc />
        public ISignals Signals => this.signals;

        public AsyncProvider(ILoggerFactory loggerFactory, ISignals signals, INodeLifetime nodeLifetime)
        {
            this.lockAsyncDelegates = new object();
            this.lockRegisteredTasks = new object();

            this.asyncDelegates = new Dictionary<IAsyncDelegate, AsyncTaskInfo>();
            this.registeredTasks = new Dictionary<Task, AsyncTaskInfo>();

            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName);

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

                this.asyncDelegates.Add(newDelegate, new AsyncTaskInfo(friendlyName, AsyncTaskInfo.AsyncTaskType.Dequeuer));
            }

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            newDelegate.ConsumerTask.ContinueWith(
                this.OnAsyncDelegateUnhandledException,
                newDelegate,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            newDelegate.ConsumerTask.ContinueWith(
                this.OnAsyncDelegateCompleted,
                newDelegate,
                CancellationToken.None,
                TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

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
                this.asyncDelegates.Add(loopInstance, new AsyncTaskInfo(name, AsyncTaskInfo.AsyncTaskType.Loop));
            }

            loopTask = loopInstance.Run(cancellation, repeatEvery ?? TimeSpan.FromMilliseconds(DefaultLoopRepeatInterval), startAfter).RunningTask;

            // task will continue with onAsyncDelegateUnhandledException if @delegate had unhandled exceptions
            loopTask.ContinueWith(
                this.OnAsyncDelegateUnhandledException,
                loopInstance,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

            // task will continue with onAsyncDelegateCompleted if @delegate completed or was canceled
            loopTask.ContinueWith(
                this.OnAsyncDelegateCompleted,
                loopInstance,
                CancellationToken.None,
                TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

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
        public Task RegisterTask(string name, Task taskToRegister)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(taskToRegister, nameof(taskToRegister));

            // instantiate the loop

            lock (this.lockRegisteredTasks)
            {
                this.registeredTasks.Add(taskToRegister, new AsyncTaskInfo(name, AsyncTaskInfo.AsyncTaskType.RegisteredTask));
            }

            // task will continue with OnRegisteredTaskUnhandledException if @delegate had unhandled exceptions
            taskToRegister.ContinueWith(
                this.OnRegisteredTaskUnhandledException,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

            // task will continue with OnRegisteredTaskCompleted if @delegate completed or was canceled
            taskToRegister.ContinueWith(
                this.OnRegisteredTaskCompleted,
                CancellationToken.None,
                TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
                );

            return taskToRegister;
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
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.Type == AsyncTaskInfo.AsyncTaskType.Dequeuer);
            }
        }

        /// <inheritdoc />
        public bool IsAsyncLoopRunning(string name)
        {
            lock (this.lockAsyncDelegates)
            {
                // task in the dictionaries are either running or faulted so we just look for a dequeuer with the given name and status not faulted.
                return this.asyncDelegates.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.Type == AsyncTaskInfo.AsyncTaskType.Loop);
            }
        }

        /// <inheritdoc />
        public bool IsRegisteredTaskRunning(string name)
        {
            lock (this.lockRegisteredTasks)
            {
                // task in the dictionaries are either running or faulted so we just look for a registered task with the given name and status not faulted.
                return this.registeredTasks.Values.Any(@delegate => @delegate.FriendlyName == name && @delegate.Status != TaskStatus.Faulted && @delegate.Type == AsyncTaskInfo.AsyncTaskType.RegisteredTask);
            }
        }

        /// <inheritdoc />
        [NoTrace]
        public string GetStatistics(bool faultyOnly)
        {
            var taskInformations = new List<AsyncTaskInfo>();
            lock (this.lockAsyncDelegates)
            {
                taskInformations.AddRange(this.asyncDelegates.Values);
            }

            lock (this.lockRegisteredTasks)
            {
                taskInformations.AddRange(this.registeredTasks.Values);
            }

            int running = taskInformations.Where(info => info.IsRunning).Count();
            int faulted = taskInformations.Where(info => !info.IsRunning).Count();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"====== Async loops ======   [Running: {running.ToString()}] [Faulted: {faulted.ToString()}]");

            if (faultyOnly && faulted == 0)
                return sb.ToString(); // If there are no faulty tasks and faultOnly is set to true, return just the header.

            var data =
                from info in taskInformations
                orderby info.FriendlyName
                select new
                {
                    Columns = new string[]
                    {
                        info.FriendlyName,
                        (info.Type.ToString()),
                        (info.IsRunning ? "Running" : "Faulted")
                    },
                    Exception = info.Exception?.Message
                };

            foreach (var item in this.benchmarkColumnsDefinition)
            {
                sb.Append(item.Name.PadRight(item.Width));
            }

            sb.AppendLine();
            sb.AppendLine("-".PadRight(this.benchmarkColumnsDefinition.Sum(column => column.Width), '-'));

            foreach (var row in data)
            {
                // skip non faulty rows (Exception is null) if faultyOnly is set.
                if (faultyOnly && row.Exception == null)
                    continue;

                for (int iColumn = 0; iColumn < this.benchmarkColumnsDefinition.Length; iColumn++)
                {
                    sb.Append(row.Columns[iColumn].PadRight(this.benchmarkColumnsDefinition[iColumn].Width));
                }

                // if exception != null means the loop is faulted, so I show the reason in a row under it, a little indented.
                if (row.Exception != null)
                {
                    sb.AppendLine();
                    sb.Append($"      * Fault Reason: {row.Exception}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("-".PadRight(this.benchmarkColumnsDefinition.Sum(column => column.Width), '-'));

            return sb.ToString();
        }

        /// <inheritdoc />
        [NoTrace]
        public List<(string loopName, TaskStatus status)> GetAll()
        {
            var taskInformation = new List<AsyncTaskInfo>();

            lock (this.lockAsyncDelegates)
            {
                taskInformation.AddRange(this.asyncDelegates.Values);
            }

            lock (this.lockRegisteredTasks)
            {
                taskInformation.AddRange(this.registeredTasks.Values);
            }

            List<(string, TaskStatus)> runningTasks = taskInformation.Select(a => (a.FriendlyName, a.Status)).OrderBy(a => a.Item1).ToList();

            return runningTasks;
        }

        private void OnRegisteredTaskCompleted(Task task)
        {
            AsyncTaskInfo itemToRemove;
            lock (this.lockRegisteredTasks)
            {
                if (this.registeredTasks.TryGetValue(task, out itemToRemove))
                {
                    this.registeredTasks.Remove(task);
                }
            }

            if (itemToRemove != null)
            {
                this.logger.LogDebug("Registered task '{0}' Removed. Id: {1}.", itemToRemove.FriendlyName, task.Id);
            }
            else
            {
                // Should never happen.
                this.logger.LogError("Cannot find the registered task with Id {0}.", task.Id);
            }
        }

        /// <summary>
        ///  This method is called when a registered Task throws an unhandled exception.
        /// </summary>
        /// <param name="task">The task causing the exception.</param>
        /// <param name="state">not used</param>
        private void OnRegisteredTaskUnhandledException(Task task)
        {
            AsyncTaskInfo delegateInfo;
            lock (this.lockRegisteredTasks)
            {
                if (this.registeredTasks.TryGetValue(task, out delegateInfo))
                {
                    // casted to IAsyncTaskInfoSetter to be able to set properties
                    IAsyncTaskInfoSetter infoSetter = delegateInfo;

                    infoSetter.Exception = task.Exception.GetBaseException();
                    infoSetter.Status = task.Status;
                }
                else
                {
                    // Should never happen.
                    this.logger.LogError("Cannot find the registered task with Id {0}.", task.Id);
                    return;
                }

                this.logger.LogError(task.Exception.GetBaseException(), "Unhandled exception for registered task {0}.", delegateInfo.FriendlyName);
            }
        }

        /// <summary>
        ///  This method is called when a Task running an <see cref="IAsyncDelegate"/> captured an unhandled exception.
        /// </summary>
        /// <param name="task">The delegate task.</param>
        /// <param name="state">The <see cref="IAsyncDelegate"/> that's run by the delegateTask</param>
        /// <remarks>state can be either of type <see cref="IAsyncDelegateDequeuer{T}"/> or <see cref="IAsyncLoop"/></remarks>
        private void OnAsyncDelegateUnhandledException(Task task, object state)
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
        private void OnAsyncDelegateCompleted(Task task, object state)
        {
            AsyncTaskInfo itemToRemove;
            lock (this.lockAsyncDelegates)
            {
                if (this.asyncDelegates.TryGetValue((IAsyncDelegate)state, out itemToRemove))
                {
                    // When AsyncLoop fails with an uncaughtException, it handle it completing fine.
                    // I want instead to keep its failed status visible on console so I handle this scenario as faulted task.
                    // TODO: discuss about this decision.
                    if (state is AsyncLoop asyncLoop && asyncLoop.UncaughtException != null)
                    {
                        // casted to IAsyncTaskInfoSetter to be able to set properties
                        IAsyncTaskInfoSetter infoSetter = itemToRemove;

                        infoSetter.Exception = asyncLoop.UncaughtException;
                        infoSetter.Status = TaskStatus.Faulted;

                        this.logger.LogError("Async Loop '{0}' completed with an UncaughtException, marking it as faulted. Task Id: {1}.", itemToRemove.FriendlyName, task.Id);
                        return;
                    }
                    else
                    {
                        this.asyncDelegates.Remove((IAsyncDelegate)state);
                    }
                }
            }

            if (itemToRemove != null)
            {
                this.logger.LogDebug("IAsyncDelegate task '{0}' Removed. Id: {1}.", itemToRemove.FriendlyName, task.Id);
            }
            else
            {
                // Should never happen.
                this.logger.LogError("Cannot find the IAsyncDelegate task with Id {0}.", task.Id);
            }
        }
    }
}
