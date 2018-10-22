using System.Collections.Generic;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Memory size cache that implements the Least Recently Used (LRU) policy.
    /// </summary>
    public class MemorySizeCache<TKey, TValue> : MemoryCache<TKey, TValue>
    {
        /// <summary>Maximum size in bytes that can be stored in the cache.</summary>
        private readonly long maxSize;

        /// <summary>Gets the size of all items in the cache, in bytes.</summary>
        public long TotalSize
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.totalSize;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCountCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="maxSize">Maximum size in bytes count that can be stored in the cache.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default comparer for the type of the key.</param>
        public MemorySizeCache(long maxSize, IEqualityComparer<TKey> comparer = null) : base(comparer)
        {
            Guard.Assert(maxSize > 0);

            this.maxSize = maxSize;
        }

        /// <summary>Create or overwrite an item in the cache.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to add to the cache.</param>
        /// <param name="size">Value size in bytes.</param>
        public void AddOrUpdate(TKey key, TValue value, long size)
        {
            var item = new MemoryCache<TKey, TValue>.CacheItem(key, value) { Size = size };

            base.AddOrUpdate(item);
        }

        protected override bool IsCacheFull(CacheItem item)
        {
            return this.TotalSize > (this.maxSize - item.Size);
        }
    }
}
