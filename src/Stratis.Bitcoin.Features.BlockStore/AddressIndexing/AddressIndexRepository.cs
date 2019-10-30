using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Repository for <see cref="AddressIndexerData"/> items with cache layer built in.</summary>
    public class AddressIndexRepository : MemorySizeCache<string, AddressIndexerData>
    {
        private const string DbAddressDataKey = "AddrData";

        private readonly LiteCollection<AddressIndexerData> addressIndexerDataCollection;

        private readonly ILogger logger;

        public AddressIndexRepository(LiteDatabase db, ILoggerFactory loggerFactory, int maxBalanceChangesToKeep = 50_000) : base(maxBalanceChangesToKeep)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.addressIndexerDataCollection = db.GetCollection<AddressIndexerData>(DbAddressDataKey);
            this.addressIndexerDataCollection.EnsureIndex("BalanceChangedHeightIndex", "$.BalanceChanges[*].BalanceChangedHeight", false);
        }

        /// <summary>Retrieves address data, either the cached version if it exists, or directly from the underlying database.
        /// If it is a previously unseen address an empty record will be created and added to the cache.</summary>
        /// <param name="address">The address to retrieve data for.</param>
        public AddressIndexerData GetOrCreateAddress(string address)
        {
            if (!this.TryGetValue(address, out AddressIndexerData data))
            {
                this.logger.LogDebug("Not found in cache.");
                data = this.addressIndexerDataCollection.FindById(address) ?? new AddressIndexerData() { Address = address, BalanceChanges = new List<AddressBalanceChange>() };
            }

            int size = 1 + data.BalanceChanges.Count / 10;
            this.AddOrUpdate(address, data, size);

            return data;
        }

        public double GetLoadPercentage()
        {
            return Math.Round(this.totalSize / (this.MaxSize / 100.0), 2);
        }

        /// <summary>
        /// Checks for addresses that are affected by balance changes above a given block height.
        /// This method should only be relied upon for block heights lower than the consensus tip and higher
        /// than (tip - maxReorg). This is because it is only used while reorging the address indexer.
        /// </summary>
        /// <param name="height">The block height above which balance changes should be considered.</param>
        /// <returns>A list of affected addresses containing balance changes above the specified block height.</returns>
        public List<string> GetAddressesHigherThanHeight(int height)
        {
            this.SaveAllItems();

            // Need to specify index name explicitly so that it gets used for the query.
            IEnumerable<AddressIndexerData> affectedAddresses = this.addressIndexerDataCollection.Find(Query.GT("BalanceChangedHeightIndex", height));

            // Per LiteDb documentation:
            // "Returning an IEnumerable your code still connected to datafile.
            // Only when you finish consume all data, datafile will be disconnected."
            return affectedAddresses.Select(x => x.Address).ToList();
        }

        /// <inheritdoc />
        protected override void ItemRemovedLocked(CacheItem item)
        {
            base.ItemRemovedLocked(item);

            if (item.Dirty)
                this.addressIndexerDataCollection.Upsert(item.Value);
        }

        public void SaveAllItems()
        {
            lock (this.LockObject)
            {
                CacheItem[] dirtyItems = this.Keys.Where(x => x.Dirty).ToArray();
                this.addressIndexerDataCollection.Upsert(dirtyItems.Select(x => x.Value));

                foreach (CacheItem dirtyItem in dirtyItems)
                    dirtyItem.Dirty = false;
            }
        }
    }
}