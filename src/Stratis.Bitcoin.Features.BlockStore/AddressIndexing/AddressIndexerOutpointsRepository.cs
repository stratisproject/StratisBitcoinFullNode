using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Repository for <see cref="OutPointData"/> items with cache layer built in.</summary>
    public sealed class AddressIndexerOutpointsRepository : MemoryCache<string, OutPointData>
    {
        private const string DbOutputsDataKey = "OutputsData";

        private const string DbOutputsRewindDataKey = "OutputsRewindData";

        /// <summary>Represents the output collection.</summary>
        /// <remarks>Should be protected by <see cref="LockObject"/></remarks>
        private readonly LiteCollection<OutPointData> addressIndexerOutPointData;

        /// <summary>Represents the rewind data collection.</summary>
        /// <remarks>Should be protected by <see cref="LockObject"/></remarks>
        private readonly LiteCollection<AddressIndexerRewindData> addressIndexerRewindData;

        private readonly ILogger logger;

        private readonly int maxCacheItems;

        public AddressIndexerOutpointsRepository(LiteDatabase db, ILoggerFactory loggerFactory, int maxItems = 60_000)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.addressIndexerOutPointData = db.GetCollection<OutPointData>(DbOutputsDataKey);
            this.addressIndexerRewindData = db.GetCollection<AddressIndexerRewindData>(DbOutputsRewindDataKey);
            this.maxCacheItems = maxItems;
        }

        public double GetLoadPercentage()
        {
            return Math.Round(this.totalSize / (this.maxCacheItems / 100.0), 2);
        }

        public void AddOutPointData(OutPointData outPointData)
        {
            this.AddOrUpdate(new CacheItem(outPointData.Outpoint, outPointData, 1));
        }

        public void RemoveOutPointData(OutPoint outPoint)
        {
            lock (this.LockObject)
            {
                if (this.Cache.TryGetValue(outPoint.ToString(), out LinkedListNode<CacheItem> node))
                {
                    this.Cache.Remove(node.Value.Key);
                    this.Keys.Remove(node);
                    this.totalSize -= 1;
                }

                if (!node.Value.Dirty)
                    this.addressIndexerOutPointData.Delete(outPoint.ToString());
            }
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

            // Not found in cache - try find it in database.
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
                CacheItem[] dirtyItems = this.Keys.Where(x => x.Dirty).ToArray();
                this.addressIndexerOutPointData.Upsert(dirtyItems.Select(x => x.Value));

                foreach (CacheItem dirtyItem in dirtyItems)
                    dirtyItem.Dirty = false;
            }
        }

        /// <summary>Persists rewind data into the repository.</summary>
        /// <param name="rewindData">The data to be persisted.</param>
        public void RecordRewindData(AddressIndexerRewindData rewindData)
        {
            lock (this.LockObject)
            {
                this.addressIndexerRewindData.Upsert(rewindData);
            }
        }

        /// <summary>Deletes rewind data items that were originated at height lower than <paramref name="height"/>.</summary>
        /// <param name="height">The threshold below which data will be deleted.</param>
        public void PurgeOldRewindData(int height)
        {
            lock (this.LockObject)
            {
                var itemsToPurge = this.addressIndexerRewindData.Find(x => x.BlockHeight < height).ToArray();

                for (int i = 0; i < itemsToPurge.Count(); i++)
                {
                    this.addressIndexerRewindData.Delete(itemsToPurge[i].BlockHash);

                    if (i % 100 == 0)
                        this.logger.LogInformation("Purging {0}/{1} rewind data items.", i, itemsToPurge.Count());
                }
            }
        }

        /// <summary>Reverts changes made by processing blocks with height higher than <param name="height">.</param></summary>
        /// <param name="height">The height above which to restore outpoints.</param>
        public void RewindDataAboveHeight(int height)
        {
            lock (this.LockObject)
            {
                IEnumerable<AddressIndexerRewindData> toRestore = this.addressIndexerRewindData.Find(x => x.BlockHeight > height);

                this.logger.LogDebug("Restoring data for {0} blocks.", toRestore.Count());

                foreach (AddressIndexerRewindData rewindData in toRestore)
                {
                    // Put the spent outputs back into the cache.
                    foreach (OutPointData outPointData in rewindData.SpentOutputs)
                        this.AddOutPointData(outPointData);

                    // This rewind data item should now be removed from the collection.
                    this.addressIndexerRewindData.Delete(rewindData.BlockHash);
                }
            }
        }

        /// <inheritdoc />
        protected override bool IsCacheFullLocked(CacheItem item)
        {
            return this.totalSize + 1 > this.maxCacheItems;
        }
    }
}
