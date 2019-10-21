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
        private int lockDepth;
        private object lockObj;

        public int WaitingThreads => this.waitingThreads;

        public DBLock()
        {
            this.waitingThreads = 0;
            this.lockObj = new object();
            this.lockLastReleasedBy = -1;
            this.lockLastAcquiredBy = -1;
            this.lockDepth = 0;
        }

        public void Release()
        {
            lock (this.lockObj)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;

                Guard.Assert(this.lockDepth >= 0);
                Guard.Assert(this.lockLastAcquiredBy != -1);
                Guard.Assert(this.lockLastReleasedBy == threadId || this.lockLastReleasedBy == -1);

                if (this.lockDepth == 1)
                    this.lockLastAcquiredBy = -1;

                this.lockDepth--;
                this.lockLastReleasedBy = threadId;
            }
        }

        public bool Wait(bool wait = true)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            this.lockDepth++;

            if (this.lockLastAcquiredBy == threadId)
                return true;

            Interlocked.Increment(ref this.waitingThreads);

            while (true)
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
