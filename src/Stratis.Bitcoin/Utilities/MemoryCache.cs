using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Memory cache that implements the Least Recently Used (LRU) standard.
    /// </summary>
    public class MemoryCache<TKey, TValue>
    {
        /// <summary>Dictionary that contains cached items.</summary>
        private readonly ConcurrentDictionary<TKey, TValue> cache;

        /// <summary>Keys sorted by their last access time with most recent ones at the end.</summary>
        private readonly LinkedList<TKey> keys;

        /// <summary>Maximum items count that can be stored in the cache.</summary>
        private readonly int sizeLimit;

        /// <summary>Amount to compact the cache by when the maximum size is exceeded.</summary>
        private readonly double compactionPercentage;

        /// <summary>Lock to protect access to <see cref="keys"/>.</summary>
        private readonly object mutex;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="sizeLimit">Maximum items count that can be stored in the cache.</param>
        /// <param name="compactionPercentage">Amount to compact the cache by when the maximum size is exceeded.</param>
        public MemoryCache(int sizeLimit, double compactionPercentage)
        {
            this.sizeLimit = sizeLimit;
            this.compactionPercentage = compactionPercentage;

            this.cache = new ConcurrentDictionary<TKey, TValue>();
            this.keys = new LinkedList<TKey>();
            this.mutex = new object();
        }

        /// <summary>Gets the count of the current entries for diagnostic purposes.</summary>
        public int Count
        {
            get
            {
                lock (this.mutex)
                {
                    return this.keys.Count;
                }
            }
        }

        /// <summary>Create or overwrite an entry in the cache.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to add to the cache.</param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            if (this.cache.TryGetValue(key, out TValue priorEntry))
            {
                this.cache.TryUpdate(key, value, priorEntry);

                lock (this.mutex)
                {
                    this.keys.Remove(key);
                    this.keys.AddLast(key);
                }
            }
            else
            {
                this.cache.TryAdd(key, value);

                lock (this.mutex)
                {
                    this.keys.AddLast(key);
                }

                if (this.Count >= this.sizeLimit)
                    this.Compact();
            }
        }

        /// <summary>Removes the object associated with the given key.</summary>
        /// <param name="key">Item's key that will be removed from the cache.</param>
        public void Remove(TKey key)
        {
            if (this.cache.TryRemove(key, out TValue unused))
            {
                lock (this.mutex)
                {
                    this.keys.Remove(key);
                }
            }
        }

        /// <summary>Gets the item associated with this key if present.</summary>
        /// <param name="key">Item's key.</param>
        /// <param name="value">Item assosiated with specified <paramref name="key"/>.</param>
        /// <returns><c>true</c> if cache contains the item; <c>false</c> otherwise.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            bool success = this.cache.TryGetValue(key, out value);

            if (success)
            {
                lock (this.mutex)
                {
                    this.keys.Remove(key);
                    this.keys.AddLast(key);
                }
            }

            return success;
        }

        /// <summary>Remove at least the given percentage (0.10 for 10%) of the total entries.</summary>
        private void Compact()
        {
            int compactItemsCount = (int)(this.sizeLimit * (1.0 - this.compactionPercentage));

            List<TKey> keysToRemove;
            lock (this.mutex)
            {
                keysToRemove = this.keys.Take(compactItemsCount).ToList();
                foreach (TKey key in keysToRemove)
                {
                    this.keys.Remove(key);
                }
            }

            foreach (TKey key in keysToRemove)
            {
                this.cache.TryRemove(key, out TValue unused);
            }
        }
    }
}
