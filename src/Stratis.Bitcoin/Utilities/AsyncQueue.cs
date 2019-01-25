using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Async queue is a thread-safe queue that can operate in callback mode or blocking dequeue mode.
    /// In callback mode it asynchronously executes a user-defined callback when a new item is added to the queue.
    /// In blocking dequeue mode, <see cref="DequeueAsync(CancellationToken)"/> is used to wait for and dequeue
    /// an item from the queue once it becomes available.
    /// <para>
    /// In callback mode, the queue guarantees that the user-defined callback is executed only once at the time.
    /// If another item is added to the queue, the callback is called again after the current execution
    /// is finished.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type of items to be inserted in the queue.</typeparam>
    public class AsyncQueue<T> : IDisposable
    {
        /// <summary>
        /// Execution context holding information about the current status of the execution
        /// in order to recognize if <see cref="Dispose"/> was called within the callback method.
        /// </summary>
        private class AsyncContext
        {
            /// <summary>
            /// Set to <c>true</c> if <see cref="Dispose"/> was called from within the callback routine,
            /// set to <c>false</c> otherwise.
            /// </summary>
            public bool DisposeRequested { get; set; }
        }

        /// <summary>
        /// Represents a callback method to be executed when a new item is added to the queue.
        /// </summary>
        /// <param name="item">Newly added item.</param>
        /// <param name="cancellationToken">Cancellation token that the callback method should use for its async operations to avoid blocking the queue during shutdown.</param>
        /// <remarks>It is allowed to call <see cref="Dispose"/> from the callback method.</remarks>
        public delegate Task OnEnqueueAsync(T item, CancellationToken cancellationToken);

        /// <summary>Lock object to protect access to <see cref="items"/>.</summary>
        private readonly object lockObject;

        /// <summary>Storage of items in the queue that are waiting to be consumed.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Queue<T> items;

        /// <summary>Event that is triggered when at least one new item is waiting in the queue.</summary>
        private readonly AsyncManualResetEvent signal;

        /// <summary>Callback routine to be called when a new item is added to the queue.</summary>
        private readonly OnEnqueueAsync onEnqueueAsync;

        /// <summary>Consumer of the items in the queue which responsibility is to execute the user defined callback.</summary>
        /// <remarks>Internal for test purposes.</remarks>
        internal readonly Task ConsumerTask;

        /// <summary>Cancellation that is triggered when the component is disposed.</summary>
        private readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>Number of pending dequeue operations which need to be finished before the queue can fully dispose.</summary>
        private volatile int unfinishedDequeueCount;

        /// <summary><c>true</c> if <see cref="Dispose"/> was called, <c>false</c> otherwise.</summary>
        private bool disposed;

        /// <summary><c>true</c> if the queue operates in callback mode, <c>false</c> if it operates in blocking dequeue mode.</summary>
        private readonly bool callbackMode;

        /// <summary>
        /// Async context to allow to recognize whether <see cref="Dispose"/> was called from within the callback routine.
        /// <para>
        /// Is not <c>null</c> if the queue is operating in callback mode and the current async execution context is the one that executes the callbacks,
        /// set to <c>null</c> otherwise.
        /// </para>
        /// </summary>
        private readonly AsyncLocal<AsyncContext> asyncContext;

        /// <summary>
        /// Initializes the queue either in blocking dequeue mode or in callback mode.
        /// </summary>
        /// <param name="onEnqueueAsync">Callback routine to be called when a new item is added to the queue, or <c>null</c> to operate in blocking dequeue mode.</param>
        public AsyncQueue(OnEnqueueAsync onEnqueueAsync = null)
        {
            this.callbackMode = onEnqueueAsync != null;
            this.lockObject = new object();
            this.items = new Queue<T>();
            this.signal = new AsyncManualResetEvent();
            this.onEnqueueAsync = onEnqueueAsync;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.asyncContext = new AsyncLocal<AsyncContext>();
            this.ConsumerTask = this.callbackMode ? this.ConsumerAsync() : null;
        }

        /// <summary>
        /// Add a new item to the queue and signal to the consumer task.
        /// </summary>
        /// <param name="item">Item to be added to the queue.</param>
        public void Enqueue(T item)
        {
            lock (this.lockObject)
            {
                this.items.Enqueue(item);
                this.signal.Set();
            }
        }

        /// <summary>
        /// The number of items in the queue.
        /// This property should only be used for collecting statistics.
        /// </summary>
        public int Count
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.items.Count;
                }
            }
        }

        /// <summary>
        /// Consumer of the newly added items to the queue that waits for the signal
        /// and then executes the user-defined callback.
        /// <para>
        /// This consumer loop is only used when the queue is operating in the callback mode.
        /// </para>
        /// </summary>
        private async Task ConsumerAsync()
        {
            // Set the context, so that Dispose called from callback will recognize it.
            this.asyncContext.Value = new AsyncContext();

            bool callDispose = false;
            CancellationToken cancellationToken = this.cancellationTokenSource.Token;
            while (!callDispose && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for an item to be enqueued.
                    await this.signal.WaitAsync(cancellationToken).ConfigureAwait(false);

                    // Dequeue all items and execute the callback.
                    T item;
                    while (this.TryDequeue(out item) && !cancellationToken.IsCancellationRequested)
                    {
                        await this.onEnqueueAsync(item, cancellationToken).ConfigureAwait(false);

                        if (this.asyncContext.Value.DisposeRequested)
                        {
                            callDispose = true;
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (callDispose) this.DisposeInternal(true);
        }

        /// <summary>
        /// Dequeues an item from the queue if there is one.
        /// If the queue is empty, the method waits until an item is available.
        /// </summary>
        /// <param name="cancellation">Cancellation token that allows aborting the wait if the queue is empty.</param>
        /// <returns>Dequeued item from the queue.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered or when the queue is disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called on a queue that operates in callback mode.</exception>
        public async Task<T> DequeueAsync(CancellationToken cancellation = default(CancellationToken))
        {
            if (this.callbackMode)
                throw new InvalidOperationException($"{nameof(this.DequeueAsync)} called on queue in callback mode.");

            // Increment the counter so that the queue's cancellation source is not disposed when we are using it.
            Interlocked.Increment(ref this.unfinishedDequeueCount);

            try
            {
                if (this.disposed)
                    throw new OperationCanceledException();

                // First check if an item is available. If it is, just return it.
                T item;
                if (this.TryDequeue(out item))
                    return item;

                // If the queue is empty, we need to wait until there is an item available.
                using (CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, this.cancellationTokenSource.Token))
                {
                    while (true)
                    {
                        await this.signal.WaitAsync(cancellationSource.Token).ConfigureAwait(false);

                        // Note that another thread could consume the message before us,
                        // so dequeue safely and loop if nothing is available.
                        if (this.TryDequeue(out item))
                            return item;
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref this.unfinishedDequeueCount);
            }
        }

        /// <summary>
        /// Dequeues an item from the queue if there is any.
        /// </summary>
        /// <param name="item">If the function succeeds, this is filled with the dequeued item.</param>
        /// <returns><c>true</c> if an item was dequeued, <c>false</c> if the queue was empty.</returns>
        public bool TryDequeue(out T item)
        {
            item = default(T);
            lock (this.lockObject)
            {
                if (this.items.Count > 0)
                {
                    item = this.items.Dequeue();

                    if (this.items.Count == 0)
                        this.signal.Reset();

                    return true;
                }

                return false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.asyncContext.Value != null)
            {
                // We can't dispose now as we would end up in deadlock because we are called from within the callback.
                // We just mark that dispose was requested and process with the dispose once we return from the callback.
                this.asyncContext.Value.DisposeRequested = true;
                return;
            }

            this.DisposeInternal(false);
        }

        /// <summary>
        /// Frees resources used by the queue and only returns until all unfinished tasks of the objects are finished.
        /// </summary>
        /// <param name="calledFromConsumerTask"><c>true</c> if this method is called from <see cref="ConsumerTask"/>, <c>false</c> otherwise.</param>
        private void DisposeInternal(bool calledFromConsumerTask)
        {
            // We do not need synchronization over this, if it is going to be missed
            // we just wait a little longer.
            this.disposed = true;

            this.cancellationTokenSource.Cancel();
            if (!calledFromConsumerTask) this.ConsumerTask?.Wait();

            if (!this.callbackMode)
            {
                // Wait until all pending dequeue operations are finished.
                // As this is very fast once disposed has been set to true,
                // we can afford busy wait.
                while (this.unfinishedDequeueCount > 0)
                    Thread.Sleep(5);
            }

            this.cancellationTokenSource.Dispose();
        }
    }
}