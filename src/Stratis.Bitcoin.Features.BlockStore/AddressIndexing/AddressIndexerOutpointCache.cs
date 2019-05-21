using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexerOutpointCache
    {
        /// <summary>This class holds a key-value pair used to represent an item
        /// in the LRU cache. It is necessary to maintain both values here so
        /// that it is possible to look up items in the cache dictionary when
        /// removing from the cache linked list.
        /// </summary>
        private class LRUItem
        {
            public LRUItem(string outPoint, ScriptPubKeyMoneyPair outPointData)
            {
                Guard.NotEmpty(outPoint, nameof(outPoint));
                Guard.NotNull(outPointData, nameof(outPointData));

                this.Key = outPoint;
                this.Value = outPointData;
            }

            public readonly string Key;

            public readonly ScriptPubKeyMoneyPair Value;
        }

        public const int AddressIndexOutputCacheMaxItemsDefault = 100000;

        private const int BatchSize = 100;

        /// <summary>The maximum number of items that can be kept in the cache until
        /// entries will start getting evicted.</summary>
        public readonly int MaxItems;

        /// <summary>A mapping between the string representation of an outpoint and its
        /// corresponding scriptPubKey and money value.
        /// All access to the cache must be protected with <see cref="lockObj"/>.</summary>
        private readonly Dictionary<string, LinkedListNode<LRUItem>> cachedOutPoints;

        /// <summary>A linked list used to efficiently determine the oldest entry
        /// in the cache. Items get added to the cache in time order and get moved to
        /// the back of the eviction list each time they are accessed.</summary>
        private readonly LinkedList<LRUItem> outPointLinkedList;

        private object lockObj;

        private readonly LiteDatabase db;

        private readonly LiteCollection<ScriptPubKeyMoneyPair> addressIndexerOutPointData;

        public AddressIndexerOutpointCache(LiteDatabase db, string addressIndexerOutputCollectionName)
        {
            this.lockObj = new object();
            this.db = db;
            this.addressIndexerOutPointData = this.db.GetCollection<ScriptPubKeyMoneyPair>(addressIndexerOutputCollectionName);

            this.MaxItems = AddressIndexOutputCacheMaxItemsDefault;

            this.cachedOutPoints = new Dictionary<string, LinkedListNode<LRUItem>>();
            this.outPointLinkedList = new LinkedList<LRUItem>();
        }

        public void AddToCache(string outPoint, ScriptPubKeyMoneyPair outPointData)
        {
            lock (this.lockObj)
            {
                // Don't bother adding the entry if it exists already.
                if (this.cachedOutPoints.ContainsKey(outPoint))
                    return;

                if (this.cachedOutPoints.Count >= this.MaxItems)
                    this.RemoveOldest();

                var item = new LRUItem(outPoint, outPointData);
                var node = new LinkedListNode<LRUItem>(item);
                this.outPointLinkedList.AddLast(node);
                this.cachedOutPoints.Add(outPoint, node);
            }
        }

        /// <summary>When an output is spent there is no point retaining it
        /// any longer in the cache or on disk.</summary>
        /// <param name="outPoint">The string representation of the outpoint
        /// to remove from cache & database.</param>
        public void Remove(string outPoint)
        {
            lock (this.lockObj)
            {
                if (!this.cachedOutPoints.TryGetValue(outPoint, out LinkedListNode<LRUItem> item))
                    return;

                this.outPointLinkedList.Remove(item);
                this.cachedOutPoints.Remove(outPoint);
                this.addressIndexerOutPointData.Delete(outPoint);
            }
        }

        public void RemoveOldest()
        {
            lock (this.lockObj)
            {
                LinkedListNode<LRUItem> node = this.outPointLinkedList.First;
                this.outPointLinkedList.RemoveFirst();
                this.cachedOutPoints.Remove(node.Value.Key);
                this.addressIndexerOutPointData.Delete(node.Value.Key);
            }
        }

        public ScriptPubKeyMoneyPair GetOutpoint(string outPoint)
        {
            lock (this.lockObj)
            {
                this.cachedOutPoints.TryGetValue(outPoint, out LinkedListNode<LRUItem> outPointData);

                if (outPointData != null)
                {
                    this.outPointLinkedList.Remove(outPointData);
                    this.outPointLinkedList.AddLast(outPointData);
                    return outPointData.Value.Value;
                }

                ScriptPubKeyMoneyPair item = this.addressIndexerOutPointData.FindById(outPoint);

                if (item != null)
                    this.AddToCache(outPoint, item);

                return item;
            }
        }

        public void Flush()
        {
            lock (this.lockObj)
            {
                var batch = new List<ScriptPubKeyMoneyPair>();

                foreach (LRUItem outPointData in this.outPointLinkedList)
                {
                    batch.Add(outPointData.Value);

                    if (batch.Count < BatchSize)
                        continue;

                    this.addressIndexerOutPointData.Upsert(batch);
                    batch.Clear();
                }

                if (batch.Count <= 0)
                    return;

                this.addressIndexerOutPointData.Upsert(batch);
                batch.Clear();
            }
        }
    }
}
