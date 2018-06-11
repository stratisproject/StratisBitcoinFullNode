using System.Collections.Generic;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Memory cache that implements the Least Recently Used (LRU) policy.
    /// </summary>
    public class MemoryCache<TKey, TValue>
    {
        /// <summary>Cache item for the inner usage of the <see cref="MemoryCache{TKey,TValue}"/> class.</summary>
        private class CacheItem
        {
            public readonly TKey Key;

            public TValue Value { get; set; }

            /// <summary>Initializes a new instance of the <see cref="CacheItem{TKey, TValue}"/> class.</summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value.</param>
            public CacheItem(TKey key, TValue value)
            {
                this.Key = key;
                this.Value = value;
            }
        }

        /// <summary>Dictionary that contains cached items.</summary>
        /// <remarks>Should be accessed inside a lock using <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> cache;

        /// <summary>Keys sorted by their last access time with most recent ones at the end.</summary>
        /// <remarks>Should be accessed inside a lock using <see cref="lockObject"/>.</remarks>
        private readonly LinkedList<CacheItem> keys;

        /// <summary>Maximum items count that can be stored in the cache.</summary>
        private readonly int maxItemsCount;
        
        /// <summary>Lock to protect access to <see cref="keys"/> and <see cref="cache"/>.</summary>
        private readonly object lockObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="maxItemsCount">Maximum items count that can be stored in the cache.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default comparer for the type of the key.</param>
        public MemoryCache(int maxItemsCount, IEqualityComparer<TKey> comparer = null)
        {
            Guard.Assert(maxItemsCount > 0);

            this.maxItemsCount = maxItemsCount;

            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(this.maxItemsCount, comparer);
            this.keys = new LinkedList<CacheItem>();
            this.lockObject = new object();
        }

        /// <summary>Gets the count of the current items for diagnostic purposes.</summary>
        public int Count
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.keys.Count;
                }
            }
        }

        /// <summary>Create or overwrite an item in the cache.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to add to the cache.</param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            LinkedListNode<CacheItem> node;

            lock (this.lockObject)
            {
                if (this.cache.TryGetValue(key, out node))
                {
                    node.Value.Value = value;
                    this.keys.Remove(node);
                }
                else
                {
                    if (this.keys.Count == this.maxItemsCount)
                    {
                        // Remove the item that was not used for the longest time.
                        LinkedListNode<CacheItem> lastNode = this.keys.First;
                        this.cache.Remove(lastNode.Value.Key);
                        this.keys.RemoveFirst();
                    }

                    node = new LinkedListNode<CacheItem>(new CacheItem(key, value));
                    this.cache.Add(key, node);
                }

                this.keys.AddLast(node);
            }
        }

        /// <summary>Removes the object associated with the given key.</summary>
        /// <param name="key">Key of that item that will be removed from the cache.</param>
        public void Remove(TKey key)
        {
            lock (this.lockObject)
            {
                if (this.cache.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    this.cache.Remove(node.Value.Key);
                    this.keys.Remove(node);
                }
            }
        }

        /// <summary>Gets an item associated with specific key if present.</summary>
        /// <param name="key">Item's key.</param>
        /// <param name="value">Item associated with specified <paramref name="key"/>.</param>
        /// <returns><c>true</c> if cache contains the item, <c>false</c> otherwise.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (this.lockObject)
            {
                if (this.cache.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    this.keys.Remove(node);
                    this.keys.AddLast(node);

                    value = node.Value.Value;

                    return true;
                }
            }

            value = default(TValue);
            return false;
        }
    }
}
