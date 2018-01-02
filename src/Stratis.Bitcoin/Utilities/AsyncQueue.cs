using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Async queue is a simple thread-safe queue that asynchronously executes 
    /// a user-defined callback when a new item is added to the queue.
    /// <para>
    /// The queue guarantees that the user-defined callback is executed only once at the time. 
    /// If another item is added to the queue, the callback is called again after the current execution 
    /// is finished.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type of items to be inserted in the queue.</typeparam>
    public class AsyncQueue<T>: IDisposable
    {
        /// <summary>
        /// Represents a callback method to be executed when a new item is added to the queue.
        /// </summary>
        /// <param name="item">Newly added item.</param>
        /// <param name="cancellationToken">Cancellation token that the callback method should use for its async operations to avoid blocking the queue during shutdown.</param>
        public delegate Task OnEnqueueAsync(T item, CancellationToken cancellationToken);

        /// <summary>Storage of items in the queue that are waiting to be consumed.</summary>
        private readonly ConcurrentQueue<T> items;

        /// <summary>Event that is triggered when at least one new item is waiting in the queue.</summary>
        private readonly AsyncManualResetEvent signal;

        /// <summary>Callback routine to be called when a new item is added to the queue.</summary>
        private readonly OnEnqueueAsync onEnqueueAsync;

        /// <summary>Consumer of the items in the queue which responsibility is to execute the user defined callback.</summary>
        private readonly Task consumerTask;

        /// <summary>Cancellation that is triggered when the component is disposed.</summary>
        private readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Initializes the queue.
        /// </summary>
        /// <param name="onEnqueueAsync">Callback routine to be called when a new item is added to the queue.</param>
        public AsyncQueue(OnEnqueueAsync onEnqueueAsync)
        {
            Guard.NotNull(onEnqueueAsync, nameof(onEnqueueAsync));

            this.items = new ConcurrentQueue<T>();
            this.signal = new AsyncManualResetEvent();
            this.onEnqueueAsync = onEnqueueAsync;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.consumerTask = ConsumerAsync();
        }

        /// <summary>
        /// Add a new item to the queue and signal to the consumer task.
        /// </summary>
        /// <param name="item">Item to be added to the queue.</param>
        public void Enqueue(T item)
        {
            this.items.Enqueue(item);
            this.signal.Set();
        }

        /// <summary>
        /// Consumer of the newly added items to the queue that waits for the signal 
        /// and then executes the user-defined callback.
        /// </summary>
        private async Task ConsumerAsync()
        {
            CancellationToken cancellationToken = this.cancellationTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for an item to be enqueued.
                    await this.signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                    this.signal.Reset();

                    // Dequeue all items and execute the callback.
                    T item;
                    while (this.items.TryDequeue(out item) && !cancellationToken.IsCancellationRequested)
                    {
                        await this.onEnqueueAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.consumerTask.Wait();
            this.cancellationTokenSource.Dispose();
        }
    }
}