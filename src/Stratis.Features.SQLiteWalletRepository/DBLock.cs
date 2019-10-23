using System;
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
        private DateTime firstClaimedAt;
        private int lastClaimedByThread;
        private int firstClaimedByThread;
        private int firstClaimedPromise;
        private string firstClaimedStackTrace;
        private int lockDepth;
        private object lockObj;

        public int WaitingThreads => this.waitingThreads;

        public bool IsAvailable => this.lastClaimedByThread == -1;

        public DBLock()
        {
            this.waitingThreads = 0;
            this.lockObj = new object();
            this.lastClaimedByThread = -1;
            this.lockDepth = 0;
        }

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

        public bool Wait(bool wait = true, int timeoutSeconds = 120, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
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

            Interlocked.Increment(ref this.waitingThreads);

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

            Interlocked.Decrement(ref this.waitingThreads);

            return this.lastClaimedByThread == threadId;
        }
    }
}
