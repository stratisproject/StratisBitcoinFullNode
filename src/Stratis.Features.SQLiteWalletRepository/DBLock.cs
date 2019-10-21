using System.Threading;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Used to lock access to database objects accross threads.
    /// </summary>
    /// <remarks>
    /// Lock behavior:
    /// 1) If the thread that acquired the lock calls Wait again these subsequent calls should be ignored.
    /// 2) If the thread that released the lock calls Release again these subsequent calls should be ignored.
    /// 3) Keeps track of the number of threads waiting to acquire the lock.
    /// </remarks>
    internal class DBLock
    {
        private int waitingThreads;
        private int lockLastAcquiredBy;
        private int lockLastReleasedBy;
        private object lockObj;

        public int WaitingThreads => this.waitingThreads;

        public DBLock()
        {
            this.waitingThreads = 0;
            this.lockObj = new object();
            this.lockLastReleasedBy = -1;
            this.lockLastAcquiredBy = -1;
        }

        public void Release()
        {
            lock (this.lockObj)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;

                if (this.lockLastReleasedBy == threadId)
                    return;

                Guard.Assert(this.lockLastAcquiredBy != -1);
                Guard.Assert(this.lockLastReleasedBy == -1);

                this.lockLastReleasedBy = threadId;
                this.lockLastAcquiredBy = -1;
            }
        }

        public bool Wait(bool wait = true)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            Interlocked.Increment(ref this.waitingThreads);

            while (this.lockLastAcquiredBy != threadId)
            {
                lock (this.lockObj)
                {
                    if (this.lockLastAcquiredBy == -1)
                    {
                        this.lockLastAcquiredBy = threadId;
                        this.lockLastReleasedBy = -1;
                        break;
                    }
                }

                if (wait)
                    Thread.Yield();
                else
                    break;
            }

            Interlocked.Decrement(ref this.waitingThreads);

            return this.lockLastAcquiredBy == threadId;
        }
    }
}
