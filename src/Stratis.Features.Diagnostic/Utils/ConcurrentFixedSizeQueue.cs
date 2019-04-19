using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Stratis.Features.Diagnostic.Utils
{
    /// <summary>
    /// Non locking Concurrent Fixed Size Queue.
    /// This implementation is a lose fixed size queue, because it may sometime exceed the number of items because it wraps a ConcurrentQueue and
    /// that is a lock-free concurrent queue implementation, so even if there is a chance it may exceed maxSize, it serves the purpose of circular buffer
    /// to hold a limited set of updated elements.
    /// </summary>
    /// <typeparam name="T">The type of collection items.</typeparam>
    /// <seealso cref="System.Collections.Generic.IReadOnlyCollection{T}" />
    /// <seealso cref="System.Collections.ICollection" />
    public class ConcurrentFixedSizeQueue<T> : IReadOnlyCollection<T>, ICollection
    {
        private readonly ConcurrentQueue<T> concurrentQueue;
        private readonly int maxSize;

        public int Count => this.concurrentQueue.Count;

        public bool IsEmpty => this.concurrentQueue.IsEmpty;

        public ConcurrentFixedSizeQueue(int maxSize) : this(Array.Empty<T>(), maxSize) { }

        public ConcurrentFixedSizeQueue(IEnumerable<T> initialCollection, int maxSize)
        {
            if (initialCollection == null)
            {
                throw new ArgumentNullException(nameof(initialCollection));
            }

            this.concurrentQueue = new ConcurrentQueue<T>(initialCollection);
            this.maxSize = maxSize;
        }

        public void Enqueue(T item)
        {
            this.concurrentQueue.Enqueue(item);

            if (this.concurrentQueue.Count > this.maxSize)
            {
                T result;
                this.concurrentQueue.TryDequeue(out result);
            }
        }

        public void TryPeek(out T result) => this.concurrentQueue.TryPeek(out result);

        public bool TryDequeue(out T result) => this.concurrentQueue.TryDequeue(out result);

        public void CopyTo(T[] array, int index) => this.concurrentQueue.CopyTo(array, index);

        public T[] ToArray() => this.concurrentQueue.ToArray();

        public IEnumerator<T> GetEnumerator() => this.concurrentQueue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #region Explicit ICollection implementations.
        void ICollection.CopyTo(Array array, int index) => ((ICollection)this.concurrentQueue).CopyTo(array, index);

        object ICollection.SyncRoot => ((ICollection)this.concurrentQueue).SyncRoot;

        bool ICollection.IsSynchronized => ((ICollection)this.concurrentQueue).IsSynchronized;
        #endregion

        public override int GetHashCode() => this.concurrentQueue.GetHashCode();

        public override bool Equals(object obj) => this.concurrentQueue.Equals(obj);

        public override string ToString() => this.concurrentQueue.ToString();
    }
}
