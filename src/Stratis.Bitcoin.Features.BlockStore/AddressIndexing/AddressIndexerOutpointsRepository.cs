using System.Collections.Generic;
using LiteDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Repository for <see cref="OutPointData"/> items with cache layer built in.</summary>
    public class AddressIndexerOutpointsRepository : MemoryCache<string, OutPointData>
    {
        private const string DbOutputsDataKey = "OutputsData";

        /// <summary>LiteDb performs updates more efficiently in batches.</summary>
        private const int SaveBatchSize = 1000;

        /// <remarks>Should be protected by <see cref="LockObject"/></remarks>
        private readonly LiteCollection<OutPointData> addressIndexerOutPointData;

        private readonly ILogger logger;

        private readonly int maxCacheItems;

        public AddressIndexerOutpointsRepository(LiteDatabase db, ILoggerFactory loggerFactory, int maxItems = 100_000)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.addressIndexerOutPointData = db.GetCollection<OutPointData>(DbOutputsDataKey);

            this.maxCacheItems = maxItems;
        }

        public void AddOutPointData(OutPointData outPointData)
        {
            this.AddOrUpdate(new CacheItem(outPointData.Outpoint.ToString(), outPointData, 1));
        }

        public void RemoveOutPointData(OutPoint outPoint, bool alsoFromDatabase = true)
        {
            lock (this.LockObject)
            {
                if (this.Cache.TryGetValue(outPoint.ToString(), out LinkedListNode<CacheItem> node))
                {
                    this.Cache.Remove(node.Value.Key);
                    this.Keys.Remove(node);
                    this.ItemRemovedLocked(node.Value);
                }

                this.totalSize -= 1;
            }

            // Remove from db. We allow the caller to decide whether or not to do this (default to true for safety).
            // This is optional to allow the caller to batch deletes for efficiency. The access patterns for
            // outpoint data are also quite different to the address index, in that an outpoint is only in the cache
            // until it is spent, upon which it is never used again. However, there are transactions with very large
            // numbers of inputs, which cause inefficient deletes on the underlying database if they are not batched.
            if (alsoFromDatabase)
                this.addressIndexerOutPointData.Delete(outPoint.ToString());
        }

        public void BulkDelete(HashSet<string> outPoints)
        {
            // TODO: This is just a stub. LiteDb has no batch deletion, we need to come up with a workaround
            foreach (string outPoint in outPoints)
                this.addressIndexerOutPointData.Delete(outPoint);
        }

        protected override void ItemRemovedLocked(CacheItem item)
        {
            base.ItemRemovedLocked(item);

            if (item.Dirty)
                this.addressIndexerOutPointData.Upsert(item.Value);
        }

        public bool TryGetOutPointData(OutPoint outPoint, out OutPointData outPointData)
        {
            if (this.TryGetValue(outPoint.ToString(), out outPointData))
            {
                this.logger.LogTrace("(-)[FOUND_IN_CACHE]:true");
                return true;
            }

            // Not found in cache - try find it in database
            outPointData = this.addressIndexerOutPointData.FindById(outPoint.ToString());

            if (outPointData != null)
            {
                this.AddOutPointData(outPointData);
                this.logger.LogTrace("(-)[FOUND_IN_DATABASE]:true");
                return true;
            }

            return false;
        }

        public void SaveAllItems()
        {
            lock (this.LockObject)
            {
                var batch = new List<OutPointData>();

                foreach (CacheItem cacheItem in this.Keys)
                {
                    if (!cacheItem.Dirty)
                        continue;

                    batch.Add(cacheItem.Value);
                    cacheItem.Dirty = false;

                    if (batch.Count < SaveBatchSize)
                        continue;

                    this.addressIndexerOutPointData.Upsert(batch);
                    batch.Clear();
                }

                if (batch.Count > 0)
                    this.addressIndexerOutPointData.Upsert(batch);
            }
        }

        /// <inheritdoc />
        protected override bool IsCacheFullLocked(CacheItem item)
        {
            return this.totalSize + 1 > this.maxCacheItems;
        }
    }
}
