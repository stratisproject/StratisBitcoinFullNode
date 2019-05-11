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

    public class LockProtected
    {
        protected object lockObject;

        public LockProtected()
        {
            this.lockObject = new object();
        }

        public void Synchronous(Action action)
        {
            lock (this.lockObject)
            {
                action.Invoke();
            }
        }

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
