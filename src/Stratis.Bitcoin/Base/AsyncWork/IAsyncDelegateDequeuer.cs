using System;

namespace Stratis.Bitcoin.Base.AsyncWork
{
    public interface IAsyncDelegateDequeuer : IDisposable { }

    /// <summary>
    /// Define a delegate that is called asynchronously in the background whenever a new <typeparamref name="T"/> is queued and run it.
    /// </summary>
    /// <typeparam name="T">Type of the queued items used in the delegate.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public interface IAsyncDelegateDequeuer<T> : IAsyncDelegateDequeuer
    {
        /// <summary>
        /// Add a new item to the queue and signal to the consumer task.
        /// </summary>
        /// <param name="item">Item to be added to the queue.</param>
        void Enqueue(T item);
    }
}