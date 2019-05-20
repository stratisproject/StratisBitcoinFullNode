using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    public class AddressIndexCache
    {
        /// <summary>Default maximum number of items (addresses and balance changes) in the cache.</summary>
        public const int AddressIndexCacheMaxItemsDefault = 100000;

        /// <summary>The number of records to be batched up and written out at once during cache flushes.</summary>
        private const int BatchSize = 100;

        /// <summary>Maximum number of items (addresses and balance changes) in the cache.</summary>
        public readonly int MaxItems;

        /// <summary>Number of address index records & associated balance changes present in the cache.</summary>
        private int itemCount;

        /// <summary>All access to the cache must be protected with <see cref="lockObj"/>.</summary>
        private readonly Dictionary<string, AddressIndexerData> cachedAddresses;

        /// <summary>The dirty address cache contains the set of addresses that have been
        /// modified since their inclusion into the cache itself. This speeds up the
        /// flushing process as only dirty addresses need to be flushed.</summary>
        /// <remarks>All access to the dirty address cache must be protected with <see cref="lockObj"/>.</remarks>
        private readonly HashSet<string> dirtyAddresses;

        private object lockObj;

        private readonly LiteDatabase db;

        private readonly LiteCollection<AddressIndexerData> addressIndexerData;

        public AddressIndexCache(LiteDatabase db, string addressIndexerCollectionName)
        {
            this.lockObj = new object();
            this.db = db;
            this.addressIndexerData = this.db.GetCollection<AddressIndexerData>(addressIndexerCollectionName);
            this.addressIndexerData.EnsureIndex("BalanceChangedHeightIndex", "$.BalanceChanges[*].BalanceChangedHeight", false);

            this.MaxItems = AddressIndexCacheMaxItemsDefault;
            this.itemCount = 0;

            this.dirtyAddresses = new HashSet<string>();

            // The cache is initially empty. A possible improvement could be
            // persisting it to disk on node shutdown.
            this.cachedAddresses = new Dictionary<string, AddressIndexerData>();
        }

        public void AddToCache(AddressIndexerData indexData)
        {
            lock (this.lockObj)
            {
                // TODO: Have a mapping of last accessed timestamps so items can age out too
                // TODO: LRU concept - use the dictionary as a lookup for index positions into an array-like data structure

                // Don't bother adding the entry if it exists already.
                if (this.cachedAddresses.ContainsKey(indexData.Address))
                    return;

                // Now check if any entries need to be evicted.

                // The size is the base record plus each balance change.
                // We need to count it this way because an address may have very
                // few changes and should therefore have less weight than one
                // with numerous changes.
                int newItemCount = indexData.BalanceChanges.Count + 1;

                int itemsToEvict = (this.itemCount + newItemCount) - this.MaxItems;

                while (this.itemCount > 0 && itemsToEvict > 0)
                {
                    // For now, evict whichever entry appears first, until LRU functionality added.
                    string addressToEvict = this.cachedAddresses.Keys.FirstOrDefault();

                    if (addressToEvict == null)
                        break;

                    // If it's been modified it needs to be persisted first, so changes aren't lost.
                    if (this.dirtyAddresses.Contains(addressToEvict))
                    {
                        this.addressIndexerData.Upsert(this.cachedAddresses[addressToEvict]);
                        this.dirtyAddresses.Remove(addressToEvict);
                    }

                    int itemToEvictSize = this.cachedAddresses[addressToEvict].BalanceChanges.Count + 1;

                    this.cachedAddresses.Remove(addressToEvict);
                    this.itemCount -= itemToEvictSize;
                    itemsToEvict -= itemToEvictSize;
                }

                this.cachedAddresses.Add(indexData.Address, indexData);
                this.itemCount += newItemCount;
            }
        }

        /// <summary>
        /// Retrieves address data, either the cached version if it exists,
        /// or directly from the underlying database. If it is a previously
        /// unseen address an empty record will be created and added to the
        /// cache.
        /// </summary>
        /// <param name="address">The address to retrieve data for.</param>
        public AddressIndexerData GetOrCreateAddress(string address)
        {
            lock (this.lockObj)
            {
                this.cachedAddresses.TryGetValue(address, out AddressIndexerData indexData);

                if (indexData != null)
                    return indexData;

                indexData = this.addressIndexerData.FindById(address);

                if (indexData == null)
                    indexData = new AddressIndexerData() { Address = address, BalanceChanges = new List<AddressBalanceChange>() };

                // Just add it, there is no need to mark it as dirty until it has balance changes.
                this.AddToCache(indexData);

                return indexData;
            }
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
            // Need to flush the cache first before searching the underlying database to ensure complete results.
            this.Flush();

            lock (this.lockObj)
            {
                // Need to specify index name explicitly so that it gets used for the query.
                IEnumerable<AddressIndexerData> affectedAddresses = this.addressIndexerData.Find(Query.GT("BalanceChangedHeightIndex", height));

                // Per LiteDb documentation:
                // "Returning an IEnumerable your code still connected to datafile.
                // Only when you finish consume all data, datafile will be disconnected."
                return affectedAddresses.Select(x => x.Address).ToList();
            }
        }

        public void Flush()
        {
            lock (this.lockObj)
            {
                var batch = new List<AddressIndexerData>();

                foreach (string address in this.dirtyAddresses)
                {
                    // TODO: This seems like the only good place to collapse balance changes into fewer entries
                    // TODO: The drawback with that being that unused indexer records on disk may then never get collapsed

                    batch.Add(this.cachedAddresses[address]);

                    if (batch.Count < BatchSize)
                        continue;

                    this.addressIndexerData.Upsert(batch);
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    this.addressIndexerData.Upsert(batch);
                    batch.Clear();
                }

                this.dirtyAddresses.Clear();
            }
        }

        public void MarkDirty(AddressIndexerData indexData)
        {
            lock (this.lockObj)
            {
                this.dirtyAddresses.Add(indexData.Address);
            }
        }
    }
}