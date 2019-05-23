using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Repository for <see cref="AddressIndexerData"/> items with cache layer built in.</summary>
    public class AddressIndexRepository : MemorySizeCache<string, AddressIndexerData>
    {
        private const string DbAddressDataKey = "AddrData";

        /// <summary>LiteDb performs updates more efficiently in batches.</summary>
        private const int SaveBatchSize = 1000;

        private readonly LiteCollection<AddressIndexerData> addressIndexerDataCollection;

        private readonly ILogger logger;

        public AddressIndexRepository(LiteDatabase db, ILoggerFactory loggerFactory, int maxItems = 100000) : base(maxItems)
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
            if (this.TryGetValue(address, out AddressIndexerData data))
            {
                this.logger.LogTrace("(-)[FOUND_IN_CACHE]");
                return data;
            }

            data = this.addressIndexerDataCollection.FindById(address) ?? new AddressIndexerData() { Address = address, BalanceChanges = new List<AddressBalanceChange>() };

            int size = data.BalanceChanges.Count + 1;
            this.AddOrUpdate(address, data, size);

            return data;
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
                var batch = new List<AddressIndexerData>();

                foreach (CacheItem cacheItem in this.Keys)
                {
                    if (!cacheItem.Dirty)
                        continue;

                    batch.Add(cacheItem.Value);

                    if (batch.Count < SaveBatchSize)
                        continue;

                    this.addressIndexerDataCollection.Upsert(batch);
                    batch.Clear();
                }

                if (batch.Count > 0)
                    this.addressIndexerDataCollection.Upsert(batch);
            }
        }
    }
}