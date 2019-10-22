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
        private int lastClaimedBy;
        private int lockDepth;
        private object lockObj;

        public int WaitingThreads => this.waitingThreads;

        public bool IsAvailable => this.lastClaimedBy == -1;

        public DBLock()
        {
            this.waitingThreads = 0;
            this.lockObj = new object();
            this.lastClaimedBy = -1;
            this.lockDepth = 0;
        }

        public void Release()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            lock (this.lockObj)
            {
                Guard.Assert(this.lockDepth > 0);
                Guard.Assert(this.lastClaimedBy != -1);

                if (this.lockDepth == 1)
                {
                    this.lastClaimedBy = -1;
                }
                else
                {
                    this.lastClaimedBy = threadId;
                }

                this.lockDepth--;
            }
        }

        public bool Wait(bool wait = true)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            lock (this.lockObj)
            {
                if (this.lastClaimedBy == threadId)
                {
                    this.lockDepth++;
                    return true;
                }
            }

            Interlocked.Increment(ref this.waitingThreads);

            while (true)
            {
                lock (this.lockObj)
                {
                    if (this.lastClaimedBy == -1)
                    {
                        this.lastClaimedBy = threadId;
                        this.lockDepth = 1;
                        break;
                    }
                }

                if (wait)
                    Thread.Yield();
                else
                    break;
            }

            Interlocked.Decrement(ref this.waitingThreads);

            return this.lastClaimedBy == threadId;
        }
    }
}
