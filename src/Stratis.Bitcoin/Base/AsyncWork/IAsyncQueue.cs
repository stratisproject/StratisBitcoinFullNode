using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Base.AsyncWork
{
    /// <summary>
    /// Defines a queue that can be dequeued asynchronously by multiple threads.
    /// </summary>
    public interface IAsyncQueue<T> : IDisposable
    {
        /// <summary>
        /// The number of items in the queue.
        /// This property should only be used for collecting statistics.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Add a new item to the queue.
        /// </summary>
        /// <param name="item">Item to be added to the queue.</param>
        void Enqueue(T item);

        /// <summary>
        /// Dequeues an item from the queue if there is any.
        /// </summary>
        /// <param name="item">If the function succeeds, this is filled with the dequeued item.</param>
        /// <returns><c>true</c> if an item was dequeued, <c>false</c> if the queue was empty.</returns>
        bool TryDequeue(out T item);

        /// <summary>
        /// Dequeues an item from the queue if there is one.
        /// If the queue is empty, the method waits until an item is available.
        /// </summary>
        /// <param name="cancellation">Cancellation token that allows aborting the wait if the queue is empty.</param>
        /// <returns>Dequeued item from the queue.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered or when the queue is disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this method is called on a queue that operates in callback mode.</exception>
        Task<T> DequeueAsync(CancellationToken cancellation = default(CancellationToken));
    }
}
