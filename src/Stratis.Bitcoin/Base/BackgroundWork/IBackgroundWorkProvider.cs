using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.BackgroundWork
{
    public interface IBackgroundWorkProvider
    {
        /// <summary>
        /// Creates a queue that can be dequeued asynchronously by multiple threads.
        /// </summary>
        /// <typeparam name="T">Type of the queued items.</typeparam>
        /// <returns></returns>
        IAsyncQueue<T> CreateAsyncQueue<T>();

        /// <summary>
        /// Creates a delegate that is called asynchronously in the background whenever a new <typeparamref name="T"/> is queued.
        /// </summary>
        /// <typeparam name="T">Type of the queued items used in the delegate.</typeparam>
        /// <param name="name">Name of the delegate.</param>
        /// <param name="delegate">The delegate.</param>
        /// <returns></returns>
        IAsyncDelegateWorker CreateAndRunAsyncDelegate<T>(string name, Func<T, CancellationToken, Task> @delegate);

        /// <summary>
        /// Creates an asynchronous loop.
        /// </summary>
        /// <typeparam name="T">Type of the queued items used in the loop.</typeparam>
        /// <param name="name">Name of the loop.</param>
        /// <param name="loop">The loop.</param>
        /// <returns></returns>
        IAsyncLoop CreateAsyncLoop<T>(string name, Func<CancellationToken, Task> loop);
    }
}