using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin
{
    public class ThreadSafeCollection<T> : IEnumerable<T>
    {
        private readonly ConcurrentDictionary<T, T> behaviors = new ConcurrentDictionary<T, T>();

        /// <summary>
        /// Add an item to the collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns>When disposed, the item is removed</returns>
        public IDisposable Add(T item)
        {
            if(item == null)
                throw new ArgumentNullException("item");

            this.OnAdding(item);

            this.behaviors.TryAdd(item, item);

            return new ActionDisposable(() => { }, () => this.Remove(item));
        }

        protected virtual void OnAdding(T obj)
        {
        }

        protected virtual void OnRemoved(T obj)
        {
        }

        public bool Remove(T item)
        {
            bool removed = this.behaviors.TryRemove(item, out T old);

            if(removed)
                this.OnRemoved(old);

            return removed;
        }

        public void Clear()
        {
            foreach(T behavior in this)
                this.Remove(behavior);
        }

        public T FindOrCreate<U>() where U : T, new()
        {
            return this.FindOrCreate<U>(() => new U());
        }

        public U FindOrCreate<U>(Func<U> create) where U : T
        {
            U result = this.OfType<U>().FirstOrDefault();
            if(result == null)
            {
                result = create();
                this.Add(result);
            }
            return result;
        }

        public U Find<U>() where U : T
        {
            return this.OfType<U>().FirstOrDefault();
        }

        public void Remove<U>() where U : T
        {
            foreach(U b in this.OfType<U>())
            {
                this.behaviors.TryRemove(b, out T behavior);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.behaviors.Select(k => k.Key).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
