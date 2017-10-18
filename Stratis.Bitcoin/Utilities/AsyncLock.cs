using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    // https://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim semaphore;
        private readonly Task<IDisposable> releaser;

        public AsyncLock()
        {
            this.semaphore = new SemaphoreSlim(1, 1);
            this.releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync()
        {
            var wait = this.semaphore.WaitAsync();

            return wait.IsCompleted ?
                        this.releaser :
                        wait.ContinueWith((_, state) => (IDisposable)state,
                            this.releaser.Result, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
