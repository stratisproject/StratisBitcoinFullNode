using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    public class MemoryCache<TKey, TValue> : IDisposable
    {
        private readonly ConcurrentDictionary<TKey, TValue> cache;

        /// <summary>Maximum items count that can be stored in the cache.</summary>
        private readonly int sizeLimit;
        /// <summary>Amount to compact the cache by when the maximum size is exceeded.</summary>
        private readonly double compactionPercentage;

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
        }

        /// <summary>Gets the count of the current entries for diagnostic purposes.</summary>
        public int Count
        {
            get { return this.cache.Count; }
        }

        /// <summary>
        /// Create or overwrite an entry in the cache.
        /// </summary>
        public void CreateEntry(TKey key, TValue value)
        {
            if (this.cache.TryGetValue(key, out TValue priorEntry))
                this.cache.TryUpdate(key, value, priorEntry);
            else
            {
                this.cache.TryAdd(key, value);

                if (this.Count >= this.sizeLimit)
                    this.Compact();
            }
        }

        /// <summary>
        /// Removes the object associated with the given key.
        /// </summary>
        public void Remove(TKey key)
        {
            this.cache.TryRemove(key, out TValue unused);
        }

        /// <summary>Gets the item associated with this key if present.</summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.cache.TryGetValue(key, out value);
        }

        /// <summary>Remove at least the given percentage (0.10 for 10%) of the total entries.</summary>
        private void Compact()
        {
            int compactItemsCount = (int)(this.sizeLimit * (1.0 - this.compactionPercentage));

            foreach (TKey key in this.cache.Keys.Take(compactItemsCount))
            {
                this.Remove(key);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
