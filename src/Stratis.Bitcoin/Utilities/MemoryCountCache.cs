using System.Collections.Generic;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Memory count cache that implements the Least Recently Used (LRU) policy.
    /// </summary>
    public class MemoryCountCache<TKey, TValue> : MemoryCache<TKey, TValue>
    {
        /// <summary>Maximum items count that can be stored in the cache.</summary>
        private readonly int maxItemsCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCountCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="maxItemsCount">Maximum items count that can be stored in the cache.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default comparer for the type of the key.</param>
        public MemoryCountCache(int maxItemsCount, IEqualityComparer<TKey> comparer = null) : base(comparer)
        {
            Guard.Assert(maxItemsCount > 0);

            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(this.maxItemsCount, comparer);

            this.maxItemsCount = maxItemsCount;
        }

        /// <summary>Create or overwrite an item in the cache.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to add to the cache.</param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            base.AddOrUpdate(new MemoryCache<TKey, TValue>.CacheItem(key, value));
        }

        /// <inheritdoc />
        protected override bool IsCacheFullLocked(CacheItem item)
        {
            return (this.keys.Count == this.maxItemsCount);
        }
    }
}
