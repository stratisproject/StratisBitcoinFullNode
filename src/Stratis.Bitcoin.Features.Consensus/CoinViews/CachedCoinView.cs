﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Cache layer for coinview prevents too frequent updates of the data in the underlying storage.
    /// </summary>
    public class CachedCoinView : ICoinView, IBackedCoinView, IDisposable
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

            /// <summary>Original state of the transaction outputs before the change. This is used for rewinding to previous state.</summary>
            public TxOut[] OriginalOutputs;
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
        public CachePerformanceCounter PerformanceCounter { get; set; }

        /// <summary>Lock object to protect access to <see cref="unspents"/>, <see cref="blockHash"/>, <see cref="cachedRewindDataList"/>, and <see cref="innerBlockHash"/>.</summary>
        private readonly AsyncLock lockobj;

        /// <summary>Hash of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 blockHash;

        /// <summary>Hash of the block headers of the tip of the underlaying coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 innerBlockHash;

        /// <summary>Coin view at one layer below this implementaiton.</summary>
        private readonly ICoinView inner;

        /// <summary>Pending list of rewind data to be persisted to a persistent storage.</summary>
        /// <remarks>All access to this list has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly List<RewindData> cachedRewindDataList;

        /// <inheritdoc />
        public ICoinView Inner
        {
            get { return this.inner; }
        }

        /// <summary>Storage of POS block information.</summary>
        private readonly StakeChainStore stakeChainStore;

        /// <summary>Information about cached items mapped by transaction IDs the cached item's unspent outputs belong to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly Dictionary<uint256, CacheItem> unspents;

        /// <summary>Number of items in the cache.</summary>
        /// <remarks>The getter violates the lock contract on <see cref="unspents"/>, but the lock here is unnecessary as the <see cref="unspents"/> is marked as readonly.</remarks>
        public int CacheEntryCount
        {
            get { return this.unspents.Count; }
        }

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Time of the last cache flush.</summary>
        private DateTime lastCacheFlushTime;
        /// <summary>
        /// Initializes instance of the object based on DBreeze based coinview.
        /// </summary>
        /// <param name="inner">Underlying coinview with database storage.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        public CachedCoinView(DBreezeCoinView inner, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, StakeChainStore stakeChainStore = null) :
            this(dateTimeProvider, loggerFactory, stakeChainStore)
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
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        /// <remarks>
        /// This is used for testing the coinview.
        /// It allows a coin view that only has in-memory entries.
        /// </remarks>
        public CachedCoinView(InMemoryCoinView inner, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, StakeChainStore stakeChainStore = null) :
            this(dateTimeProvider, loggerFactory, stakeChainStore)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <summary>
        /// Initializes instance of the object based.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        private CachedCoinView(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, StakeChainStore stakeChainStore = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChainStore = stakeChainStore;
            this.MaxItems = CacheMaxItemsDefault;
            this.lockobj = new AsyncLock();
            this.unspents = new Dictionary<uint256, CacheItem>();
            this.PerformanceCounter = new CachePerformanceCounter(this.dateTimeProvider);
            this.lastCacheFlushTime = this.dateTimeProvider.GetUtcNow();
            this.cachedRewindDataList = new List<RewindData>();
        }

        /// <inheritdoc />
        public async Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            if (this.blockHash == null)
            {
                FetchCoinsResponse response = await this.FetchCoinsAsync(new uint256[0], cancellationToken).ConfigureAwait(false);

                this.innerBlockHash = response.BlockHash;
                this.blockHash = this.innerBlockHash;
            }

            this.logger.LogTrace("(-):'{0}'", this.blockHash);
            return this.blockHash;
        }

        /// <inheritdoc />
        public async Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(txIds, nameof(txIds));
            this.logger.LogTrace("({0}.{1}:{2})", nameof(txIds), nameof(txIds.Length), txIds.Length);

            FetchCoinsResponse result = null;
            var outputs = new UnspentOutputs[txIds.Length];
            var miss = new List<int>();
            var missedTxIds = new List<uint256>();
            using (await this.lockobj.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                for (int i = 0; i < txIds.Length; i++)
                {
                    CacheItem cache;
                    if (!this.unspents.TryGetValue(txIds[i], out cache))
                    {
                        this.logger.LogTrace("Cache missed for transaction ID '{0}'.", txIds[i]);
                        miss.Add(i);
                        missedTxIds.Add(txIds[i]);
                    }
                    else
                    {
                        this.logger.LogTrace("Cache hit for transaction ID '{0}'.", txIds[i]);
                        outputs[i] = cache.UnspentOutputs == null ? null :
                                     cache.UnspentOutputs.IsPrunable ? null :
                                     cache.UnspentOutputs.Clone();
                    }
                }

                this.PerformanceCounter.AddMissCount(miss.Count);
                this.PerformanceCounter.AddHitCount(txIds.Length - miss.Count);
            }

            this.logger.LogTrace("{0} cache missed transaction needs to be loaded from underlying CoinView.", missedTxIds.Count);
            FetchCoinsResponse fetchedCoins = await this.Inner.FetchCoinsAsync(missedTxIds.ToArray(), cancellationToken).ConfigureAwait(false);

            using (await this.lockobj.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                uint256 innerblockHash = fetchedCoins.BlockHash;
                if (this.blockHash == null)
                {
                    Debug.Assert(this.unspents.Count == 0);
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
                    cache.UnspentOutputs = unspent;
                    cache.OriginalOutputs = unspent?.Outputs.ToArray();
                    this.unspents.TryAdd(txIds[index], cache);
                }
                result = new FetchCoinsResponse(outputs, this.blockHash);
            }

            int cacheEntryCount = this.CacheEntryCount;
            if (cacheEntryCount > this.MaxItems)
            {
                this.logger.LogTrace("Cache is full now with {0} entries, evicting ...", cacheEntryCount);
                await this.EvictAsync().ConfigureAwait(false);
            }

            this.logger.LogTrace("(-):*.{0}='{1}',*.{2}.{3}={4}", nameof(result.BlockHash), result.BlockHash, nameof(result.UnspentOutputs), nameof(result.UnspentOutputs.Length), result.UnspentOutputs.Length);
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
        public async Task FlushAsync(bool force = true)
        {
            this.logger.LogTrace("({0}:{1})", nameof(force), force);

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
                await this.stakeChainStore.FlushAsync(true);

            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetTipHashAsync().ConfigureAwait(false);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if (this.innerBlockHash == null)
                {
                    this.logger.LogTrace("(-)[NULL_INNER_TIP]");
                    return;
                }

                KeyValuePair<uint256, CacheItem>[] unspent = this.unspents.Where(u => u.Value.IsDirty).ToArray();

                List<TxOut[]> originalOutputs = unspent.Select(u => u.Value.OriginalOutputs).ToList();
                foreach (KeyValuePair<uint256, CacheItem> u in unspent)
                {
                    u.Value.IsDirty = false;
                    u.Value.ExistInInner = true;
                    u.Value.OriginalOutputs = u.Value.UnspentOutputs?.Outputs.ToArray();
                }

                await this.Inner.SaveChangesAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), originalOutputs, this.innerBlockHash, this.blockHash, this.cachedRewindDataList).ConfigureAwait(false);

                // Remove prunable entries from cache as they were flushed down.
                IEnumerable<KeyValuePair<uint256, CacheItem>> prunableEntries = unspent.Where(c => (c.Value.UnspentOutputs != null) && c.Value.UnspentOutputs.IsPrunable);
                foreach (KeyValuePair<uint256, CacheItem> entry in prunableEntries)
                    this.unspents.Remove(entry.Key);

                this.cachedRewindDataList.Clear();
                this.innerBlockHash = this.blockHash;
            }

            this.lastCacheFlushTime = this.dateTimeProvider.GetUtcNow();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Deletes some items from the cache to free space for new items.
        /// Only items that are persisted in the underlaying storage can be deleted from the cache.
        /// </summary>
        private async Task EvictAsync()
        {
            this.logger.LogTrace("()");

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                // TODO: Do not create new random source every time.
                var rand = new Random();
                foreach (KeyValuePair<uint256, CacheItem> entry in this.unspents.ToList())
                {
                    if (!entry.Value.IsDirty)
                    {
                        if (rand.Next() % 3 == 0)
                        {
                            this.logger.LogTrace("Transaction ID '{0}' selected to be removed from the cache.", entry.Key);
                            this.unspents.Remove(entry.Key);
                        }
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, List<RewindData> rewindDataList = null)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));
            this.logger.LogTrace("({0}.Count():{1},{2}.Count():{3},{4}:'{5}',{6}:'{7}')", nameof(unspentOutputs), unspentOutputs.Count(), nameof(originalOutputs), originalOutputs?.Count(), nameof(oldBlockHash), oldBlockHash, nameof(nextBlockHash), nextBlockHash);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if ((this.blockHash != null) && (oldBlockHash != this.blockHash))
                {
                    this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                    throw new InvalidOperationException("Invalid oldBlockHash");
                }

                this.blockHash = nextBlockHash;
                var rewindData = new RewindData(nextBlockHash);

                foreach (UnspentOutputs unspent in unspentOutputs)
                {
                    if (this.unspents.TryGetValue(unspent.TransactionId, out CacheItem cacheItem))
                    {
                        // We'll need to restore the original outputs, so we clone it
                        // and save it in rewind data.
                        UnspentOutputs clone = unspent.Clone();
                        clone.Outputs = cacheItem.UnspentOutputs?.Outputs.ToArray() ?? Array.Empty<TxOut>();
                        rewindData.OutputsToRestore.Add(clone);

                        this.logger.LogTrace("Outputs of transaction ID '{0}' are in cache already, updating them.", unspent.TransactionId);
                        if (cacheItem.UnspentOutputs != null) cacheItem.UnspentOutputs.Spend(unspent);
                        else cacheItem.UnspentOutputs = unspent;
                    }
                    else
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' not found in cache, inserting them.", unspent.TransactionId);
                        cacheItem = new CacheItem();
                        cacheItem.ExistInInner = !unspent.IsFull; // Seems to be a new created coin (careful, untrue if rewinding).
                        cacheItem.ExistInInner |= duplicateTransactions.Any(t => unspent.TransactionId == t);
                        cacheItem.IsDirty = true;
                        cacheItem.UnspentOutputs = unspent;
                        this.unspents.Add(unspent.TransactionId, cacheItem);
                        rewindData.TransactionsToRemove.Add(unspent.TransactionId);
                    }

                    cacheItem.IsDirty = true;

                    // Inner does not need to know pruned unspent that it never saw.
                    if (cacheItem.UnspentOutputs.IsPrunable && !cacheItem.ExistInInner)
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' are prunable and not in underlaying coinview, removing from cache.", unspent.TransactionId);
                        this.unspents.Remove(unspent.TransactionId);
                    }
                }

                this.cachedRewindDataList.Add(rewindData);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task<uint256> Rewind()
        {
            this.logger.LogTrace("()");

            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetTipHashAsync().ConfigureAwait(false);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                // Check if rewind data is available in local cache. If it is
                // we can rewind and there is no need to check underlying storage.
                if (this.cachedRewindDataList.Count > 0)
                {
                    RewindData lastRewindData = this.cachedRewindDataList.Last();

                    this.RemoveTransactions(lastRewindData);
                    this.RestoreOutputs(lastRewindData);

                    // Change current block hash to the one from the rewind data.
                    this.blockHash = lastRewindData.PreviousBlockHash;

                    this.cachedRewindDataList.RemoveAt(this.cachedRewindDataList.Count - 1);
                    this.logger.LogTrace("(-)[REMOVED_FROM_BATCH]:'{0}'", this.blockHash);
                    return this.blockHash;
                }

                // Rewind data was not found in cache, try underlying storage.
                uint256 hash = await this.inner.Rewind().ConfigureAwait(false);
                this.innerBlockHash = hash;
                this.blockHash = hash;

                this.logger.LogTrace("(-):'{0}'", hash);
                return hash;
            }
        }

        private void RestoreOutputs(RewindData rewindData)
        {
            this.logger.LogTrace("()");

            foreach (UnspentOutputs unspentToRestore in rewindData.OutputsToRestore)
            {
                this.logger.LogTrace("Outputs of transaction ID '{0}' will be restored.", unspentToRestore.TransactionId);

                if (this.unspents.TryGetValue(unspentToRestore.TransactionId, out CacheItem cacheItem))
                {
                    cacheItem.UnspentOutputs = unspentToRestore;
                    cacheItem.IsDirty = true;
                }
                else
                {
                    this.logger.LogTrace("Outputs of transaction ID '{0}' not found in cache, inserting them.", unspentToRestore.TransactionId);

                    cacheItem = new CacheItem
                    {
                        UnspentOutputs = unspentToRestore,
                        IsDirty = true
                    };

                    this.unspents.Add(unspentToRestore.TransactionId, cacheItem);
                }
            }

            this.logger.LogTrace("(-)");
        }

        private void RemoveTransactions(RewindData rewindData)
        {
            this.logger.LogTrace("()");

            foreach (uint256 transactionToRemove in rewindData.TransactionsToRemove)
            {
                this.logger.LogTrace("Attempt to remove transaction with ID '{0}'.", transactionToRemove);
                this.unspents.Remove(transactionToRemove);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();
        }
    }
}
