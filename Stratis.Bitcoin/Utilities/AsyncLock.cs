using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// An async synchronization primitive that allows the caller to await inside the critical section.
    /// <para>
    /// The lock is disposable, which allows the caller to use the convenient <c>using</c> statement 
    /// and avoid caring about releasing the lock.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// private AsyncLock asyncLock = new AsyncLock();
    /// ...
    /// using (await asyncLock.LockAsync(cancellationToken))
    /// {
    ///     // Body of critical section.
    ///     ...
    ///     // Unlocking is automatic in Dispose method invoked by using statement.
    /// }
    /// </code>
    /// </example>
    /// <remarks>Based on https://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx .</remarks>
    public sealed class AsyncLock
    {
        /// <summary>
        /// Disposable mechanism that is attached to the parent lock and releases it when it is disposed.
        /// This allows the user of the lock to use the convenient <c>using</c> statement and avoid 
        /// caring about manual releasing of the lock.
        /// </summary>
        private sealed class Releaser : IDisposable
        {
            /// <summary>Parent lock to be released when this releaser is disposed.</summary>
            private readonly AsyncLock toRelease;

            /// <summary>
            /// Connects the releaser with its parent lock.
            /// </summary>
            internal Releaser(AsyncLock toRelease)
            {
                this.toRelease = toRelease;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                this.toRelease.semaphore.Release();
            }
        }

        /// <summary>Internal synchronization primitive used as a mutex to only allow one thread to occupy the critical section.</summary>
        private readonly SemaphoreSlim semaphore;

        /// <summary>Helper object that allows implementation of disposable lock for convenient use with <c>using</c> statement.</summary>
        private readonly Task<IDisposable> releaser;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public AsyncLock()
        {
            this.semaphore = new SemaphoreSlim(1, 1);
            this.releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        /// <summary>
        /// Acquires the lock.
        /// </summary>
        /// <param name="cancel">Cancellation token that can be used to abort waiting for the lock.</param>
        /// <returns>Disposable interface to enable using construct. Disposing it releases the lock.</returns>
        public Task<IDisposable> LockAsync(CancellationToken cancel = default(CancellationToken))
        {
            Task wait = this.semaphore.WaitAsync(cancel);

            if (wait.IsCompleted)
                return this.releaser;

            return wait.ContinueWith((_, state) => (IDisposable)state, this.releaser.Result, 
                cancel, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}