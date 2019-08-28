using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Cache layer for coinview prevents too frequent updates of the data in the underlying storage.
    /// </summary>
    public class CachedCoinView : ICoinView, IBackedCoinView
    {
        /// <summary>
        /// Item of the coinview cache that holds information about the unspent outputs
        /// as well as the status of the item in relation to the underlying storage.
        /// </summary>
        private class CacheItem
        {
            /// <summary>Information about transaction's outputs. Spent outputs are nulled.</summary>
            public UnspentOutputs UnspentOutputs;

            /// <summary><c>true</c> if the unspent output information is stored in the underlying storage, <c>false</c> otherwise.</summary>
            public bool ExistInInner;

            /// <summary><c>true</c> if the information in the cache is different than the information in the underlying storage.</summary>
            public bool IsDirty;
        }

        /// <summary>
        /// Hashes of transactions that have been put into multiple blocks before fully spent.
        /// <para>
        /// Historically, these two transactions violated rules that are currently applied
        /// in Bitcoin consensus. This was only possible for coinbase transactions when the miner
        /// used the same target address to receive the reward. Miners were not required to add
        /// an additional entropy (block height) to the coinbase transaction, which could result
        /// in the same hash output.
        /// </para>
        /// <para>
        /// BIP 0030 and BIP 0034 were then introduced to limit such behavior in the future.
        /// </para>
        /// </summary>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0030.mediawiki"/>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0034.mediawiki"/>
        private static readonly uint256[] duplicateTransactions = new[]
        {
            new uint256("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468"),
            new uint256("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599")
        };

        /// <summary>Default maximum number of transactions in the cache.</summary>
        public const int CacheMaxItemsDefault = 100000;

        /// <summary>Length of the coinview cache flushing interval in seconds.</summary>
        /// <seealso cref="lastCacheFlushTime"/>
        public const int CacheFlushTimeIntervalSeconds = 60;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Maximum number of transactions in the cache.</summary>
        public int MaxItems { get; set; }

        /// <summary>Statistics of hits and misses in the cache.</summary>
        private CachePerformanceCounter performanceCounter { get; set; }

        /// <summary>Lock object to protect access to <see cref="cachedUtxoItems"/>, <see cref="blockHash"/>, <see cref="cachedRewindDataIndex"/>, and <see cref="innerBlockHash"/>.</summary>
        private readonly object lockobj;

        /// <summary>Hash of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 blockHash;

        /// <summary>Height of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private int blockHeight;

        /// <summary>Hash of the block headers of the tip of the underlaying coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 innerBlockHash;

        /// <summary>Coin view at one layer below this implementaiton.</summary>
        private readonly ICoinView inner;

        /// <summary>Pending list of rewind data to be persisted to a persistent storage.</summary>
        /// <remarks>All access to this list has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly SortedDictionary<int, RewindData> cachedRewindDataIndex;

        /// <inheritdoc />
        public ICoinView Inner => this.inner;

        /// <summary>Storage of POS block information.</summary>
        private readonly StakeChainStore stakeChainStore;

        /// <summary>
        /// The rewind data index store.
        /// </summary>
        private readonly IRewindDataIndexCache rewindDataIndexCache;

        /// <summary>Information about cached items mapped by transaction IDs the cached item's unspent outputs belong to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly Dictionary<uint256, CacheItem> cachedUtxoItems;

        /// <summary>Number of items in the cache.</summary>
        /// <remarks>The getter violates the lock contract on <see cref="cachedUtxoItems"/>, but the lock here is unnecessary as the <see cref="cachedUtxoItems"/> is marked as readonly.</remarks>
        private int cacheEntryCount => this.cachedUtxoItems.Count;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Time of the last cache flush.</summary>
        private DateTime lastCacheFlushTime;

        private CachePerformanceSnapshot latestPerformanceSnapShot;

        private readonly Random random;

        /// <summary>
        /// Initializes instance of the object based on DBreeze based coinview.
        /// </summary>
        /// <param name="inner">Underlying coinview with database storage.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeStats">The node stats.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        /// <param name="rewindDataIndexCache">Rewind data index store.</param>
        public CachedCoinView(DBreezeCoinView inner, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats, StakeChainStore stakeChainStore = null, IRewindDataIndexCache rewindDataIndexCache = null) :
            this(dateTimeProvider, loggerFactory, nodeStats, stakeChainStore, rewindDataIndexCache)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <summary>
        /// Initializes instance of the object based on memory based coinview.
        /// </summary>
        /// <param name="inner">Underlying coinview with memory based storage.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeStats">The node stats.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        /// <param name="rewindDataIndexCache">Rewind data index store.</param>
        /// <remarks>
        /// This is used for testing the coinview.
        /// It allows a coin view that only has in-memory entries.
        /// </remarks>
        public CachedCoinView(InMemoryCoinView inner, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats, StakeChainStore stakeChainStore = null, IRewindDataIndexCache rewindDataIndexCache = null) :
            this(dateTimeProvider, loggerFactory, nodeStats, stakeChainStore, rewindDataIndexCache)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <summary>
        /// Initializes instance of the object based.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeStats">The node stats.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        /// <param name="rewindDataIndexCache">Rewind data index store.</param>
        private CachedCoinView(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats, StakeChainStore stakeChainStore = null, IRewindDataIndexCache rewindDataIndexCache = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChainStore = stakeChainStore;
            this.rewindDataIndexCache = rewindDataIndexCache;
            this.MaxItems = CacheMaxItemsDefault;
            this.lockobj = new object();
            this.cachedUtxoItems = new Dictionary<uint256, CacheItem>();
            this.performanceCounter = new CachePerformanceCounter(this.dateTimeProvider);
            this.lastCacheFlushTime = this.dateTimeProvider.GetUtcNow();
            this.cachedRewindDataIndex = new SortedDictionary<int, RewindData>();
            this.random = new Random();

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, this.GetType().Name, 300);
        }

        /// <inheritdoc />
        public uint256 GetTipHash(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.blockHash == null)
            {
                FetchCoinsResponse response = this.FetchCoins(new uint256[0], cancellationToken);

                this.innerBlockHash = response.BlockHash;
                this.blockHash = this.innerBlockHash;
            }

            return this.blockHash;
        }

        /// <inheritdoc />
        public FetchCoinsResponse FetchCoins(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(txIds, nameof(txIds));

            FetchCoinsResponse result = null;
            var outputs = new UnspentOutputs[txIds.Length];
            var miss = new List<int>();
            var missedTxIds = new List<uint256>();

            lock (this.lockobj)
            {
                for (int i = 0; i < txIds.Length; i++)
                {
                    CacheItem cache;
                    if (!this.cachedUtxoItems.TryGetValue(txIds[i], out cache))
                    {
                        this.logger.LogDebug("Transaction '{0}' not found in cache.", txIds[i]);
                        miss.Add(i);
                        missedTxIds.Add(txIds[i]);
                    }
                    else
                    {
                        this.logger.LogDebug("Transaction '{0}' found in cache, UTXOs:'{1}'.", txIds[i], cache.UnspentOutputs);
                        outputs[i] = cache.UnspentOutputs == null ? null :
                                     cache.UnspentOutputs.IsPrunable ? null :
                                     cache.UnspentOutputs.Clone();
                    }
                }

                this.performanceCounter.AddMissCount(miss.Count);
                this.performanceCounter.AddHitCount(txIds.Length - miss.Count);

                FetchCoinsResponse fetchedCoins = null;

                if (missedTxIds.Count > 0 || this.blockHash == null)
                {
                    this.logger.LogDebug("{0} cache missed transaction needs to be loaded from underlying CoinView.", missedTxIds.Count);
                    fetchedCoins = this.Inner.FetchCoins(missedTxIds.ToArray(), cancellationToken);
                }

                if (this.blockHash == null)
                {
                    uint256 innerblockHash = fetchedCoins.BlockHash;

                    Debug.Assert(this.cachedUtxoItems.Count == 0);
                    this.innerBlockHash = innerblockHash;
                    this.blockHash = this.innerBlockHash;
                }

                for (int i = 0; i < miss.Count; i++)
                {
                    int index = miss[i];
                    UnspentOutputs unspent = fetchedCoins.UnspentOutputs[i];
                    outputs[index] = unspent;
                    var cache = new CacheItem();
                    cache.ExistInInner = unspent != null;
                    cache.IsDirty = false;
                    cache.UnspentOutputs = unspent?.Clone();

                    this.logger.LogDebug("CacheItem added to the cache, Transaction Id '{0}', UTXO:'{1}'.", txIds[index], cache.UnspentOutputs);
                    this.cachedUtxoItems.TryAdd(txIds[index], cache);
                }

                result = new FetchCoinsResponse(outputs, this.blockHash);

                int cacheEntryCount = this.cacheEntryCount;
                if (cacheEntryCount > this.MaxItems)
                {
                    this.logger.LogDebug("Cache is full now with {0} entries, evicting.", cacheEntryCount);
                    this.EvictLocked();
                }
            }

            return result;
        }

        /// <summary>
        /// Finds all changed records in the cache and persists them to the underlying coinview.
        /// </summary>
        /// <param name="force"><c>true</c> to enforce flush, <c>false</c> to flush only if <see cref="lastCacheFlushTime"/> is older than <see cref="CacheFlushTimeIntervalSeconds"/>.</param>
        /// <remarks>
        /// WARNING: This method can only be run from <see cref="ConsensusLoop.Execute(System.Threading.CancellationToken)"/> thread context
        /// or when consensus loop is stopped. Otherwise, there is a risk of race condition when the consensus loop accepts new block.
        /// </remarks>
        public void Flush(bool force = true)
        {
            DateTime now = this.dateTimeProvider.GetUtcNow();
            if (!force && ((now - this.lastCacheFlushTime).TotalSeconds < CacheFlushTimeIntervalSeconds))
            {
                this.logger.LogTrace("(-)[NOT_NOW]");
                return;
            }

            // Before flushing the coinview persist the stake store
            // the stake store depends on the last block hash
            // to be stored after the stake store is persisted.
            if (this.stakeChainStore != null)
                this.stakeChainStore.Flush(true);

            // Before flushing the coinview persist the rewind data index store as well.
            if (this.rewindDataIndexCache != null)
                this.rewindDataIndexCache.Flush(this.blockHeight);

            if (this.innerBlockHash == null)
                this.innerBlockHash = this.inner.GetTipHash();

            lock (this.lockobj)
            {
                if (this.innerBlockHash == null)
                {
                    this.logger.LogTrace("(-)[NULL_INNER_TIP]");
                    return;
                }

                KeyValuePair<uint256, CacheItem>[] unspent = this.cachedUtxoItems.Where(u => u.Value.IsDirty).ToArray();

                foreach (KeyValuePair<uint256, CacheItem> u in unspent)
                {
                    u.Value.IsDirty = false;
                    u.Value.ExistInInner = true;
                }

                this.Inner.SaveChanges(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), null, this.innerBlockHash, this.blockHash, 0, this.cachedRewindDataIndex.Select(c => c.Value).ToList());

                // Remove prunable entries from cache as they were flushed down.
                IEnumerable<KeyValuePair<uint256, CacheItem>> prunableEntries = unspent.Where(c => (c.Value.UnspentOutputs != null) && c.Value.UnspentOutputs.IsPrunable);
                foreach (KeyValuePair<uint256, CacheItem> entry in prunableEntries)
                {
                    this.cachedUtxoItems.Remove(entry.Key);
                    this.logger.LogDebug("Removed prunable entry Transaction Id '{0}', CacheItem:'{1}'.", entry.Key, entry.Value.UnspentOutputs);
                }

                this.cachedRewindDataIndex.Clear();
                this.innerBlockHash = this.blockHash;
            }

            this.lastCacheFlushTime = this.dateTimeProvider.GetUtcNow();
        }

        /// <summary>
        /// Deletes some items from the cache to free space for new items.
        /// Only items that are persisted in the underlaying storage can be deleted from the cache.
        /// </summary>
        /// <remarks>Should be protected by <see cref="lockobj"/>.</remarks>
        private void EvictLocked()
        {
            foreach (KeyValuePair<uint256, CacheItem> entry in this.cachedUtxoItems.ToList())
            {
                if (!entry.Value.IsDirty && entry.Value.ExistInInner)
                {
                    if ((this.random.Next() % 3) == 0)
                    {
                        this.logger.LogDebug("Transaction Id '{0}' selected to be removed from the cache, CacheItem:'{1}'.", entry.Key, entry.Value.UnspentOutputs);
                        this.cachedUtxoItems.Remove(entry.Key);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void SaveChanges(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            lock (this.lockobj)
            {
                if ((this.blockHash != null) && (oldBlockHash != this.blockHash))
                {
                    this.logger.LogDebug("{0}:'{1}'", nameof(this.blockHash), this.blockHash);
                    this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                    throw new InvalidOperationException("Invalid oldBlockHash");
                }

                this.blockHeight = height;
                this.blockHash = nextBlockHash;
                var rewindData = new RewindData(oldBlockHash);
                var indexItems = new Dictionary<OutPoint, int>();

                foreach (UnspentOutputs unspent in unspentOutputs)
                {
                    if (!this.cachedUtxoItems.TryGetValue(unspent.TransactionId, out CacheItem cacheItem))
                    {
                        // This can happen very rarely in the case where we fetch items from
                        // disk but immediately call the Evict method which then removes the cached item(s).

                        this.logger.LogDebug("Outputs of transaction ID '{0}' are not found in cache, creating them.", unspent.TransactionId);

                        FetchCoinsResponse result = this.inner.FetchCoins(new[] { unspent.TransactionId });

                        UnspentOutputs unspentOutput = result.UnspentOutputs[0];

                        cacheItem = new CacheItem();
                        cacheItem.ExistInInner = unspentOutput != null;
                        cacheItem.IsDirty = false;
                        cacheItem.UnspentOutputs = unspentOutput?.Clone();

                        this.cachedUtxoItems.TryAdd(unspent.TransactionId, cacheItem);
                        this.logger.LogDebug("CacheItem added to the cache during save '{0}'.", cacheItem.UnspentOutputs);
                    }

                    // If cacheItem.UnspentOutputs is null this means the trx was not stored in the disk,
                    // that means the trx (and UTXO) is new and all the UTXOs need to be stored in cache
                    // otherwise we store to cache only the UTXO that have been spent.

                    if (cacheItem.UnspentOutputs != null)
                    {
                        // To handle rewind we'll need to restore the original outputs,
                        // so we clone it and save it in rewind data.
                        UnspentOutputs clone = unspent.Clone();

                        // We take the original items that are in cache and put them in rewind data.
                        clone.Outputs = cacheItem.UnspentOutputs.Outputs.ToArray();

                        this.logger.LogDebug("Modifying transaction '{0}' in OutputsToRestore rewind data.", unspent.TransactionId);
                        rewindData.OutputsToRestore.Add(clone);

                        this.logger.LogDebug("Cache item before spend {0}:'{1}'.", nameof(cacheItem.UnspentOutputs), cacheItem.UnspentOutputs);

                        // Now modify the cached items with the mutated data.
                        cacheItem.UnspentOutputs.Spend(unspent);

                        this.logger.LogDebug("Cache item after spend {0}:'{1}'.", nameof(cacheItem.UnspentOutputs), cacheItem.UnspentOutputs);

                        if (this.rewindDataIndexCache != null)
                        {
                            for (int i = 0; i < unspent.Outputs.Length; i++)
                            {
                                // Only push to rewind data index UTXOs that are spent.
                                // Checks against array length are needed in case problem like 3965 on VSTS appears.
                                if ((i < clone.Outputs.Length) && (unspent.Outputs[i] == null) && (clone.Outputs[i] != null))
                                {
                                    var key = new OutPoint(unspent.TransactionId, i);
                                    indexItems[key] = this.blockHeight;
                                }
                            }
                        }
                    }
                    else
                    {
                        // New trx so it needs to be deleted if a rewind happens.
                        this.logger.LogDebug("Adding transaction '{0}' to TransactionsToRemove rewind data.", unspent.TransactionId);
                        rewindData.TransactionsToRemove.Add(unspent.TransactionId);

                        // Put in the cache the new UTXOs.
                        this.logger.LogDebug("Setting {0} to {1}: '{2}'.", nameof(cacheItem.UnspentOutputs), nameof(unspent), unspent);
                        cacheItem.UnspentOutputs = unspent;
                    }

                    cacheItem.IsDirty = true;

                    // Inner does not need to know pruned unspent that it never saw.
                    if (cacheItem.UnspentOutputs.IsPrunable && !cacheItem.ExistInInner)
                    {
                        this.logger.LogDebug("Outputs of transaction ID '{0}' are prunable and not in underlaying coinview, removing from cache.", unspent.TransactionId);
                        this.cachedUtxoItems.Remove(unspent.TransactionId);
                    }
                }

                if (this.rewindDataIndexCache != null && indexItems.Any())
                {
                    this.rewindDataIndexCache.Save(indexItems);
                    this.rewindDataIndexCache.Flush(this.blockHeight);
                }

                this.cachedRewindDataIndex.Add(this.blockHeight, rewindData);
            }
        }

        /// <inheritdoc />
        public uint256 Rewind()
        {
            if (this.innerBlockHash == null)
            {
                this.innerBlockHash = this.inner.GetTipHash();
            }

            // Flush the entire cache before rewinding
            this.Flush(true);

            lock (this.lockobj)
            {
                uint256 hash = this.inner.Rewind();

                foreach (KeyValuePair<uint256, CacheItem> cachedUtxoItem in this.cachedUtxoItems)
                {
                    // This is a protection check to ensure we are
                    // not deleting dirty items form the cache.

                    if (cachedUtxoItem.Value.IsDirty)
                        throw new InvalidOperationException("Items in cache are modified");
                }

                // All the cached utxos are now on disk so we can clear the cached entry list.
                this.cachedUtxoItems.Clear();

                this.innerBlockHash = hash;
                this.blockHash = hash;
                this.blockHeight -= 1;

                if (this.rewindDataIndexCache != null)
                    this.rewindDataIndexCache.Initialize(this.blockHeight, this);

                return hash;
            }
        }

        /// <inheritdoc />
        public RewindData GetRewindData(int height)
        {
            if (this.cachedRewindDataIndex.TryGetValue(height, out RewindData existingRewindData))
                return existingRewindData;

            return this.Inner.GetRewindData(height);
        }

        [NoTrace]
        private void AddBenchStats(StringBuilder log)
        {
            log.AppendLine("======CachedCoinView Bench======");

            log.AppendLine("Cache entries".PadRight(20) + this.cacheEntryCount);

            CachePerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                log.AppendLine(snapShot.ToString());
            else
                log.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;
        }
    }
}
