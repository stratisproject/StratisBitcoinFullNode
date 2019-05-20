using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexerOutpointCache
    {
        public const int AddressIndexOutputCacheMaxItemsDefault = 100000;

        private const int BatchSize = 100;

        public int MaxItems { get; set; }

        /// <summary>Number of outpoints currently in the cache.</summary>
        private int itemCount;

        /// <summary>A mapping between the string representation of an outpoint and its
        /// corresponding scriptPubKey and money value.
        /// All access to the cache must be protected with <see cref="lockObj"/>.</summary>
        private Dictionary<string, ScriptPubKeyMoneyPair> cachedOutPoints;

        private object lockObj;

        private LiteDatabase db;

        private LiteCollection<ScriptPubKeyMoneyPair> addressIndexerOutPointData;

        public AddressIndexerOutpointCache(LiteDatabase db, string addressIndexerOutputCollectionName)
        {
            this.lockObj = new object();
            this.db = db;
            this.addressIndexerOutPointData = this.db.GetCollection<ScriptPubKeyMoneyPair>(addressIndexerOutputCollectionName);

            this.MaxItems = AddressIndexOutputCacheMaxItemsDefault;
            this.itemCount = 0;

            this.cachedOutPoints = new Dictionary<string, ScriptPubKeyMoneyPair>();
        }

        public void AddToCache(string outPoint, ScriptPubKeyMoneyPair outPointData)
        {
            lock (this.lockObj)
            {
                // Don't bother adding the entry if it exists already.
                if (this.cachedOutPoints.ContainsKey(outPoint))
                    return;

                // Now check if any entries need to be evicted, supposing a new entry is added.
                int itemsToEvict = (this.itemCount + 1) - this.MaxItems;

                while (this.itemCount > 0 && (itemsToEvict > 0))
                {
                    // For now, evict whichever entry appears first, until LRU functionality added.
                    string outPointToEvict = this.cachedOutPoints.Keys.FirstOrDefault();

                    if (outPointToEvict == null)
                        break;
                    
                    this.cachedOutPoints.Remove(outPointToEvict);
                    this.itemCount--;
                    itemsToEvict--;
                }

                this.cachedOutPoints.Add(outPoint, outPointData);
                this.itemCount++;
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
                this.cachedOutPoints.Remove(outPoint);
                this.addressIndexerOutPointData.Delete(outPoint);
            }
        }

        public ScriptPubKeyMoneyPair GetOutpoint(string outPoint)
        {
            lock (this.lockObj)
            {
                this.cachedOutPoints.TryGetValue(outPoint, out ScriptPubKeyMoneyPair outPointData);

                if (outPointData != null)
                    return outPointData;

                outPointData = this.addressIndexerOutPointData.FindById(outPoint);

                if (outPointData != null)
                    this.AddToCache(outPoint, outPointData);

                return outPointData;
            }
        }

        public void Flush()
        {
            lock (this.lockObj)
            {
                var batch = new List<ScriptPubKeyMoneyPair>();

                foreach (ScriptPubKeyMoneyPair outPointData in this.cachedOutPoints.Values)
                {
                    batch.Add(outPointData);

                    if (batch.Count < BatchSize)
                        continue;

                    this.addressIndexerOutPointData.Upsert(batch);
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    this.addressIndexerOutPointData.Upsert(batch);
                    batch.Clear();
                }
            }
        }
    }
}
