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
        private readonly Dictionary<TKey, LinkedListNode<CacheItem<TKey, TValue>>> cache;

        /// <summary>Keys sorted by their last access time with most recent ones at the end.</summary>
        private readonly LinkedList<CacheItem<TKey, TValue>> keys;

        /// <summary>Maximum items count that can be stored in the cache.</summary>
        private readonly int maxItemsCount;
        
        /// <summary>Lock to protect access to <see cref="keys"/> and <see cref="cache"/>.</summary>
        private readonly object mutex;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="maxItemsCount">Maximum items count that can be stored in the cache.</param>
        public MemoryCache(int maxItemsCount)
        {
            Guard.Assert(maxItemsCount > 0);

            this.maxItemsCount = maxItemsCount;

            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem<TKey, TValue>>>(this.maxItemsCount);
            this.keys = new LinkedList<CacheItem<TKey, TValue>>();
            this.mutex = new object();
        }

        public MemoryCache(int maxItemsCount, IEqualityComparer<TKey> comparer) : this (maxItemsCount)
        {
            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem<TKey, TValue>>>(this.maxItemsCount, comparer);
        }

        /// <summary>Gets the count of the current items for diagnostic purposes.</summary>
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
            LinkedListNode<CacheItem<TKey, TValue>> node;

            lock (this.mutex)
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
                        // Remove 1 item.
                        this.cache.Remove(this.keys.First.Value.Key);
                        this.keys.RemoveFirst();
                    }

                    node = new LinkedListNode<CacheItem<TKey, TValue>>(new CacheItem<TKey, TValue>(key, value));
                    this.cache.Add(key, node);
                }

                this.keys.AddLast(node);
            }
        }

        /// <summary>Removes the object associated with the given key.</summary>
        /// <param name="key">Item's key that will be removed from the cache.</param>
        public void Remove(TKey key)
        {
            lock (this.mutex)
            {
                if (this.cache.TryGetValue(key, out LinkedListNode<CacheItem<TKey, TValue>> node))
                {
                    this.cache.Remove(node.Value.Key);
                    this.keys.Remove(node);
                }
            }
        }

        /// <summary>Gets the item associated with this key if present.</summary>
        /// <param name="key">Item's key.</param>
        /// <param name="value">Item assosiated with specified <paramref name="key"/>.</param>
        /// <returns><c>true</c> if cache contains the item; <c>false</c> otherwise.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (this.mutex)
            {
                if (this.cache.TryGetValue(key, out LinkedListNode<CacheItem<TKey, TValue>> node))
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
        
        private class CacheItem<TKey, TValue>
        {
            public TKey Key { get; }

            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                this.Key = key;
                this.Value = value;
            }
        }
    }
}
