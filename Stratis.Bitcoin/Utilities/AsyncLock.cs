using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Usage: using(await myAsyncLock.LockAsync())
    /// Implementation: https://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx
    /// </summary>
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim semaphore;
        private readonly Task<IDisposable> releaser;

        public AsyncLock()
        {
            this.semaphore = new SemaphoreSlim(1, 1);
            this.releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync(CancellationToken cancel = default(CancellationToken))
        {
            Task wait = this.semaphore.WaitAsync(cancel);

            if (wait.IsCompleted)
            {
                return this.releaser;
            }
            else
            {
                return wait.ContinueWith((_, state) =>
                    (IDisposable)state,
                    this.releaser.Result, cancel,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private sealed class Releaser : IDisposable
        {
            private readonly AsyncLock toRelease;

            internal Releaser(AsyncLock toRelease)
            {
                this.toRelease = toRelease;
            }

            public void Dispose()
            {
                this.toRelease.semaphore.Release();
            }
        }
    }
}