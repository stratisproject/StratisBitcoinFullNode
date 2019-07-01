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
            public TKey Key { get; private set; }

            public TValue Value { get; set; }

            /// <summary>Indicates whether the item has been modified.</summary>
            public bool Dirty { get; set; }

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
        /// <remarks>Should be accessed inside a lock using <see cref="LockObject"/>.</remarks>
        protected Dictionary<TKey, LinkedListNode<CacheItem>> Cache { get; set; }

        /// <summary>Keys sorted by their last access time with most recent ones at the end.</summary>
        /// <remarks>Should be accessed inside a lock using <see cref="LockObject"/>.</remarks>
        protected LinkedList<CacheItem> Keys { get; private set; }

        /// <summary>Lock to protect access to <see cref="Keys"/> and <see cref="Cache"/>.</summary>
        protected object LockObject { get; private set; }

        /// <summary>Total size in bytes stored in the cache.</summary>
        protected long totalSize { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys, or <c>null</c> to use the default comparer for the type of the key.</param>
        public MemoryCache(IEqualityComparer<TKey> comparer = null)
        {
            this.Cache = new Dictionary<TKey, LinkedListNode<CacheItem>>();
            this.Keys = new LinkedList<CacheItem>();
            this.LockObject = new object();
        }

        /// <summary>Determine whether the cache has reached its limit.</summary>
        /// <returns><c>true</c> if cache contains the item, <c>false</c> otherwise.</returns>
        protected abstract bool IsCacheFullLocked(CacheItem item);

        /// <summary>An item was added to the cache.</summary>
        protected virtual void ItemAddedLocked(CacheItem item)
        {
            this.totalSize += item.Size;
        }

        /// <summary>An item was removed from the cache.</summary>
        protected virtual void ItemRemovedLocked(CacheItem item)
        {
            this.totalSize -= item.Size;
        }

        /// <summary>Gets the count of the current items for diagnostic purposes.</summary>
        public int Count
        {
            get
            {
                lock (this.LockObject)
                {
                    return this.Keys.Count;
                }
            }
        }

        /// <summary>Create or overwrite an item in the cache.</summary>
        /// <param name="item"><see cref="CacheItem"/> to add or update the cache.</param>
        protected virtual void AddOrUpdate(CacheItem item)
        {
            LinkedListNode<CacheItem> node;

            lock (this.LockObject)
            {
                bool insertLast = true;

                if (this.Cache.TryGetValue(item.Key, out node))
                {
                    node.Value.Value = item.Value;

                    if (node.Next == null)
                    {
                        // Already last item.
                        insertLast = false;
                    }
                    else
                        this.Keys.Remove(node);
                }
                else
                {
                    while (this.IsCacheFullLocked(item))
                    {
                        // Remove the item that was not used for the longest time.
                        LinkedListNode<CacheItem> lastNode = this.Keys.First;
                        this.Cache.Remove(lastNode.Value.Key);
                        this.Keys.RemoveFirst();

                        this.ItemRemovedLocked(lastNode.Value);
                    }

                    node = new LinkedListNode<CacheItem>(item);
                    node.Value.Size = item.Size;

                    this.Cache.Add(item.Key, node);
                    this.ItemAddedLocked(item);
                }

                node.Value.Dirty = true;

                if (insertLast)
                    this.Keys.AddLast(node);
            }
        }

        /// <summary>Removes the object associated with the given key.</summary>
        /// <param name="key">Key of that item that will be removed from the cache.</param>
        public void Remove(TKey key)
        {
            LinkedListNode<CacheItem> node = null;

            lock (this.LockObject)
            {
                if (this.Cache.TryGetValue(key, out node))
                {
                    this.Cache.Remove(node.Value.Key);
                    this.Keys.Remove(node);
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
            lock (this.LockObject)
            {
                if (this.Cache.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    this.Keys.Remove(node);
                    this.Keys.AddLast(node);

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
            lock (this.LockObject)
            {
                this.Keys.Clear();
                this.Cache.Clear();
                this.totalSize = 0;
            }
        }
    }
}
