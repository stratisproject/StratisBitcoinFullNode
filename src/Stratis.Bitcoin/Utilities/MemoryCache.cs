using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Memory cache that implements the Least Recently Used (LRU) policy.
    /// </summary>
    public abstract class MemoryCache<TKey, TValue>
    {
        /// <summary>Cache item for the inner usage of the <see cref="MemoryCountCache{TKey,TValue}"/> class.</summary>
        protected class CacheItem
        {
            public readonly TKey Key;

            public TValue Value { get; set; }

            /// <summary>Size of value in bytes.</summary>
            public long Size { get; set; }

            /// <summary>Initializes a new instance of the <see cref="CacheItem"/> class.</summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value.</param>
            public CacheItem(TKey key, TValue value)
            {
                this.Key = key;
                this.Value = value;
            }

            /// <summary>Initializes a new instance of the <see cref="CacheItem"/> class.</summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value.</param>
            public CacheItem(TKey key, TValue value, long size) : this(key, value)
            {
                this.Size = size;
            }
        }

        /// <summary>Dictionary that contains cached items.</summary>
        /// <remarks>Should be accessed inside a lock using <see cref="lockObject"/>.</remarks>
        protected Dictionary<TKey, LinkedListNode<CacheItem>> cache;

        /// <summary>Keys sorted by their last access time with most recent ones at the end.</summary>
        /// <remarks>Should be accessed inside a lock using <see cref="lockObject"/>.</remarks>
        protected readonly LinkedList<CacheItem> keys;

        /// <summary>Lock to protect access to <see cref="keys"/> and <see cref="cache"/>.</summary>
        protected readonly object lockObject;

        /// <summary>Total size in bytes stored in the cache.</summary>
        protected long totalSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default comparer for the type of the key.</param>
        public MemoryCache(IEqualityComparer<TKey> comparer = null)
        {
            this.cache = new Dictionary<TKey, LinkedListNode<CacheItem>>();
            this.keys = new LinkedList<CacheItem>();
            this.lockObject = new object();
        }

        /// <summary>Determine whether the cache has reached its limit.</summary>
        /// <returns><c>true</c> if cache contains the item, <c>false</c> otherwise.</returns>
        protected abstract bool IsCacheFullLocked(CacheItem item);

        /// <summary>An item was added to the cache.</summary>
        protected virtual void ItemAddedLocked(CacheItem item)
        {
        }

        /// <summary>An item was removed from the cache.</summary>
        protected virtual void ItemRemovedLocked(CacheItem item)
        {
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
        /// <param name="item"><see cref="CacheItem"/> to add or update the cache.</param>
        protected virtual void AddOrUpdate(CacheItem item)
        {
            LinkedListNode<CacheItem> node;

            lock (this.lockObject)
            {
                if (this.cache.TryGetValue(item.Key, out node))
                {
                    node.Value.Value = item.Value;
                    this.keys.Remove(node);
                }
                else
                {
                    while (this.IsCacheFullLocked(item))
                    {
                        // Remove the item that was not used for the longest time.
                        LinkedListNode<CacheItem> lastNode = this.keys.First;
                        this.cache.Remove(lastNode.Value.Key);
                        this.keys.RemoveFirst();

                        this.ItemRemovedLocked(lastNode.Value);
                    }

                    node = new LinkedListNode<CacheItem>(item);
                    node.Value.Size = item.Size;

                    this.cache.Add(item.Key, node);
                    this.ItemAddedLocked(item);
                }

                this.keys.AddLast(node);
            }
        }

        /// <summary>Removes the object associated with the given key.</summary>
        /// <param name="key">Key of that item that will be removed from the cache.</param>
        public void Remove(TKey key)
        {
            LinkedListNode<CacheItem> node = null;

            lock (this.lockObject)
            {
                if (this.cache.TryGetValue(key, out node))
                {
                    this.cache.Remove(node.Value.Key);
                    this.keys.Remove(node);
                    this.ItemRemovedLocked(node.Value);
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

        /// <summary>
        /// Flush the entire cache.
        /// </summary>
        public void ClearCache()
        {
            lock (this.lockObject)
            {
                this.keys.Clear();
                this.cache.Clear();
                this.totalSize = 0;
            }
        }
    }
}
