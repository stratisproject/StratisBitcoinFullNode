using System;
using System.Threading;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Used to lock access to database objects across threads.
    /// </summary>
    /// <remarks>
    /// Lock behavior:
    /// 1) If the thread that acquired the lock calls Wait again these subsequent calls should be ignored apart from incrementing the lock depth.
    /// 2) When calling Release decrease the lock depth and only act when lock depth is 1.
    /// 3) Keeps track of the number of threads waiting to acquire the lock.
    /// </remarks>
    internal class DBLock
    {
        private int waitingThreads;
        private DateTime firstClaimedAt;
        private int lastClaimedByThread;
        private int firstClaimedByThread;
        private int firstClaimedPromise;
        private string firstClaimedStackTrace;
        private int lockDepth;
        private object lockObj;

        /// <summary>The number of threads waiting to acquire the lock.</summary>
        public int WaitingThreads => this.waitingThreads;

        /// <summary>Indicates whether the lock is available.</summary>
        public bool IsAvailable => this.lastClaimedByThread == -1;

        public DBLock()
        {
            this.waitingThreads = 0;
            this.lockObj = new object();
            this.lastClaimedByThread = -1;
            this.lockDepth = 0;
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <remarks>
        /// When calling Release decrease the lock depth and only act when lock depth is 1.
        /// </remarks>
        public void Release()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            lock (this.lockObj)
            {
                Guard.Assert(this.lockDepth > 0);
                Guard.Assert(this.lastClaimedByThread != -1);

                if (this.lockDepth == 1)
                {
                    this.lastClaimedByThread = -1;
                }
                else
                {
                    this.lastClaimedByThread = threadId;
                }

                this.lockDepth--;
            }
        }

        /// <summary>
        /// Acquires the lock.
        /// </summary>
        /// <param name="wait">Set to <c>true</c> to wait until the lock becomes available. Otherwise returns immediately.</param>
        /// <param name="timeoutSeconds">The maximum time that the lock will be held.</param>
        /// <returns>Return <c>true</c> if the lock could be acquired and <c>false</c> otherwise.</returns>
        /// <remarks>
        /// If the thread that acquired the lock calls Wait again these subsequent calls should be ignored apart from incrementing the lock depth.
        /// </remarks>
        public bool Wait(bool wait = true, int timeoutSeconds = 120)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            lock (this.lockObj)
            {
                if (this.lastClaimedByThread == threadId)
                {
                    this.lockDepth++;
                    return true;
                }
            }

            if (wait)
                Interlocked.Increment(ref this.waitingThreads);

            try
            {
                while (true)
                {
                    lock (this.lockObj)
                    {
                        if (this.lastClaimedByThread == -1)
                        {
                            this.firstClaimedStackTrace = System.Environment.StackTrace;
                            this.firstClaimedAt = DateTime.Now;
                            this.firstClaimedPromise = timeoutSeconds;
                            this.firstClaimedByThread = threadId;
                            this.lastClaimedByThread = threadId;
                            this.lockDepth = 1;
                            break;
                        }
                        else if (this.firstClaimedAt.AddSeconds(this.firstClaimedPromise) <= DateTime.Now)
                            throw new SystemException($"Lock held by thread {this.firstClaimedByThread} has not been released after {this.firstClaimedPromise} seconds as promised. The stack trace when the lock was taken is '{this.firstClaimedStackTrace}`.");
                    }

                    if (wait)
                        Thread.Yield();
                    else
                        break;
                }
            }
            finally
            {
                if (wait)
                    Interlocked.Decrement(ref this.waitingThreads);
            }

            return this.lastClaimedByThread == threadId;
        }
    }
}
