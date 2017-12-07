using System;
using System.Threading;

namespace NBitcoin
{
    /// <summary>
    /// Wraps ReaderWriterLockSlim with disposable interface so that
    /// it is possible to use using construct to avoid forgotten lock releases.
    /// </summary>
    public class ReaderWriterLock
    {
        /// <summary>Internal lock object that the class wraps around.</summary>
        private ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Enters the reader's lock.
        /// </summary>
        /// <returns>Disposable interface to enable using construct. Disposing it releases the lock.</returns>
        /// <seealso cref="ReaderWriterLockSlim.EnterReadLock"/>
        /// <seealso cref="ReaderWriterLockSlim.ExitReadLock"/>
        public IDisposable LockRead()
        {
            return new ActionDisposable(() => this.rwLock.EnterReadLock(), () => this.rwLock.ExitReadLock());
        }

        /// <summary>
        /// Enters the writer's lock.
        /// </summary>
        /// <returns>Disposable interface to enable using construct. Disposing it releases the lock.</returns>
        /// <seealso cref="ReaderWriterLockSlim.EnterWriteLock"/>
        /// <seealso cref="ReaderWriterLockSlim.ExitWriteLock"/>
        public IDisposable LockWrite()
        {
            return new ActionDisposable(() => this.rwLock.EnterWriteLock(), () => this.rwLock.ExitWriteLock());
        }

        /// <summary>
        /// Enters the writer's lock.
        /// </summary>
        /// <returns>Disposable interface to enable using construct.</returns>
        /// <seealso cref="ReaderWriterLockSlim.EnterReadLock"/>
        internal bool TryLockWrite(out IDisposable locked)
        {
            locked = null;
            if (this.rwLock.TryEnterWriteLock(0))
            {
                locked = new ActionDisposable(() =>
                {
                }, () => this.rwLock.ExitWriteLock());
                return true;
            }
            return false;
        }
    }
}
