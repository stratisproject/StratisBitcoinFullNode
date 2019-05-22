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

        public void RemoveOutPointData(OutPoint outPoint)
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

            // Remove from db.
            this.addressIndexerOutPointData.Delete(outPoint.ToString());
        }

        protected override void ItemRemovedLocked(CacheItem item)
        {
            base.ItemRemovedLocked(item);

            this.addressIndexerOutPointData.Upsert(item.Value);
        }

        public bool TryGetOutPointData(OutPoint outPoint, out OutPointData outPointData)
        {
            if (this.TryGetValue(outPoint.ToString(), out outPointData))
            {
                this.logger.LogTrace("(-)[FOUND_IN_CACHE]:true");
                return true;
            }

            return false;
        }

        public void SaveAllItems()
        {
            lock (this.LockObject)
            {
                foreach (CacheItem cacheItem in this.Keys)
                    this.addressIndexerOutPointData.Upsert(cacheItem.Value);
            }
        }

        /// <inheritdoc />
        protected override bool IsCacheFullLocked(CacheItem item)
        {
            return this.totalSize + 1 > this.maxCacheItems;
        }
    }
}
