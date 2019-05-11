using System;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Supports object-level locking and allows external work in the context of the locks.
    /// </summary>
    public interface ILockProtected
    {
        /// <summary>Allows external work within lock context.</summary>
        T Synchronous<T>(Func<T> action);

        /// <summary>Allows external work within lock context.</summary>
        void Synchronous(Action action);
    }

    public class LockProtected : ILockProtected
    {
        protected object lockObject { get; private set; }

        public LockProtected()
        {
            this.lockObject = new object();
        }

        /// <inheritdoc />
        public void Synchronous(Action action)
        {
            lock (this.lockObject)
            {
                action.Invoke();
            }
        }

        /// <inheritdoc />
        public T Synchronous<T>(Func<T> action)
        {
            lock (this.lockObject)
            {
                T res = action.Invoke();

                return res;
            }
        }
    }
}
