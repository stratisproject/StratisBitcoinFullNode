using System.Collections.Generic;
using System.Threading;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Used to lock access to database objects accross threads.
    /// </summary>
    internal class DBLock
    {
        private readonly SemaphoreSlim slimLock;
        private Dictionary<int, int> depths;
        private int waitingThreads;

        public int WaitingThreads => this.waitingThreads;

        public DBLock()
        {
            this.slimLock = new SemaphoreSlim(1, 1);
            this.depths = new Dictionary<int, int>();
            this.waitingThreads = 0;
        }

        public void Release()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (this.depths.TryGetValue(threadId, out int depth))
            {
                if (depth > 0)
                {
                    this.depths[threadId] = depth - 1;
                    return;
                }

                this.depths.Remove(threadId);
            }

            this.slimLock.Release();
        }

        public bool Wait(int millisecondsTimeout = int.MaxValue)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (this.depths.TryGetValue(threadId, out int depth))
            {
                this.depths[threadId] = depth + 1;
                return true;
            }

            Interlocked.Increment(ref this.waitingThreads);
            bool res = this.slimLock.Wait(millisecondsTimeout);
            Interlocked.Decrement(ref this.waitingThreads);

            if (!res)
                return false;

            this.depths[threadId] = 0;

            return true;
        }
    }
}
