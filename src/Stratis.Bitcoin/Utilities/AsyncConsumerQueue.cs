using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Represents a queue of items that are processed when a certain condition is met or when a timer runs out.
    /// </summary>
    public interface IAsyncConsumerQueue<T> : IDisposable
    {
        /// <summary>Adds object to the end of the queue.</summary>
        void Enqueue(T item);
    }

    /// <inheritdoc />
    public class AsyncConsumerQueue<T> : IAsyncConsumerQueue<T>
    {
        private readonly ConcurrentQueue<T> queue;

        private readonly Action<ConcurrentQueue<T>> processingAction;
        private readonly Func<T, bool> immediateProcessingTrigger;
        private readonly TimeSpan? timer;
        private readonly bool restartTimerIfNotAllItemsConsumed;

        private readonly AsyncManualResetEvent trigger;

        /// <summary>Task that waits for <see cref="trigger"/> to be triggered.</summary>
        private Task triggerAwaiterTask;

        /// <summary>Task that waits until specified delay runs out.</summary>
        private Task timerAwaiterTask;

        /// <summary>Task that runs execution flow.</summary>
        private Task executor;

        /// <summary>Cancellation token source.</summary>
        private CancellationTokenSource cancellation;

        private readonly object lockObject;

        /// <summary>Queue items count.</summary>
        public int Count => this.queue.Count;

        /// <summary>
        /// Creates new instance of <see cref="AsyncConsumerQueue{T}"/>.
        /// </summary>
        /// <param name="processingAction">Action that consumes items from the queue and removes them when they are consumed.</param>
        /// <param name="immediateProcessingTrigger">Condition that has to be met in order to start processing action immediately.</param>
        /// <param name="timer">Maximum amount of time that can pass since adding item to the queue before it gets consumed.</param>
        /// <param name="restartTimerIfNotAllItemsConsumed">If set to <c>true</c> timer will be automatically restarted if not all items were consumed.</param>
        public AsyncConsumerQueue(
            Action<ConcurrentQueue<T>> processingAction,
            Func<T, bool> immediateProcessingTrigger = null,
            TimeSpan? timer = null,
            bool restartTimerIfNotAllItemsConsumed = true)
        {
            this.queue = new ConcurrentQueue<T>();
            this.processingAction = processingAction;
            this.immediateProcessingTrigger = immediateProcessingTrigger;
            this.timer = timer;
            this.restartTimerIfNotAllItemsConsumed = restartTimerIfNotAllItemsConsumed;

            this.trigger = new AsyncManualResetEvent(false);
            this.cancellation = new CancellationTokenSource();
            this.lockObject = new object();
        }

        /// <inheritdoc />
        public void Enqueue(T item)
        {
            this.queue.Enqueue(item);

            Task.Run(() =>
            {
                // If the trigger condition exists and it is met - set the trigger.
                // If no timer or condition is provided- trigger.
                if (((this.immediateProcessingTrigger != null) && this.immediateProcessingTrigger(item)) ||
                    ((this.immediateProcessingTrigger == null) && (this.timer == null)))
                {
                    this.trigger.Set();
                }
                else
                {
                    // Using 'lock' to prevent starting timer several times.
                    lock (this.lockObject)
                    {
                        // Start timer if it's not already started and timer timespan is not 'null'.
                        if (this.timer != null && (this.timerAwaiterTask == null))
                            SetTimer();
                    }
                }

                // Using 'lock' to prevent starting executor task several times.
                lock (this.lockObject)
                {
                    // Ensure flow is running.
                    if (this.executor == null || this.executor.IsCompleted)
                        this.executor = ExecutionFlowAsync();
                }

            }).ConfigureAwait(false);
        }

        private void SetTimer()
        {
            this.timerAwaiterTask = Task.Delay(this.timer.Value, this.cancellation.Token);
        }

        private async Task ExecutionFlowAsync()
        {
            // Create task that waits for the trigger event.
            if (this.triggerAwaiterTask == null || this.triggerAwaiterTask.IsCompleted)
                this.triggerAwaiterTask = this.trigger.WaitAsync(this.cancellation.Token);

            List<Task> awaitedTasks = new List<Task>();
            awaitedTasks.Add(this.triggerAwaiterTask);

            if (this.timerAwaiterTask != null && !this.timerAwaiterTask.IsCompleted)
                awaitedTasks.Add(this.timerAwaiterTask);

            // Wait until trigger is set or timer task is completed.
            await Task.WhenAny(awaitedTasks).ConfigureAwait(false);

            // Reset trigger.
            if (this.trigger.IsSet)
                this.trigger.Reset();

            // Process queue if task wasn't canceled.
            if (awaitedTasks.Any(x => x.Status == TaskStatus.RanToCompletion))
                this.processingAction(this.queue);

            // Timer is no longer needed.
            this.timerAwaiterTask = null;

            if (this.restartTimerIfNotAllItemsConsumed &&
                (this.timer != null) &&
                (this.Count != 0) &&
                !this.cancellation.IsCancellationRequested)
            {
                // Restart timer and recursively call execution flow.
                SetTimer();
                await ExecutionFlowAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.cancellation.Cancel();
            this.executor.Wait();

            this.cancellation.Dispose();
        }
    }
}
