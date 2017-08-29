using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Cache layer for coinview prevents too frequent updates of the data in the underlaying storage.
    /// </summary>
    public class CachedCoinView : CoinView, IBackedCoinView
    {
        /// <summary>
        /// Item of the coinview cache that holds information about the unspent outputs 
        /// as well as the status of the item in relation to the underlaying storage.
        /// </summary>
        private class CacheItem
        {
            /// <summary>Information about transaction's outputs. Spent outputs are nulled.</summary>
            public UnspentOutputs UnspentOutputs;

            /// <summary><c>true</c> if the unspent output information is stored in the underlaying storage, <c>false</c> otherwise.</summary>
            public bool ExistInInner;

            /// <summary><c>true</c> if the information in the cache is different than the information in the underlaying storage.</summary>
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

        /// <summary>Maximum number of transactions in the cache.</summary>
        public int MaxItems { get; set; }

        /// <summary>Statistics of hits and misses in the cache.</summary>
        public CachePerformanceCounter PerformanceCounter { get; set; }

        /// <summary>Lock object to protect access to <see cref="unspents"/>, <see cref="blockHash"/>, and <see cref="innerBlockHash"/>.</summary>
        private readonly ReaderWriterLock lockobj;

        /// <summary>Hash of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 blockHash;

        /// <summary>Hash of the block headers of the tip of the underlaying coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 innerBlockHash;


        /// <summary>Coin view at one layer below this implementaiton.</summary>
        private readonly CoinView inner;
        /// <inheritdoc />
        public CoinView Inner { get { return this.inner; } }

        /// <summary>Storage of POS block information.</summary>
        private readonly StakeChainStore stakeChainStore;

        /// <summary>Information about cached items mapped by transaction IDs the cached item's unspent outputs belong to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly Dictionary<uint256, CacheItem> unspents;

        /// <summary>Number of items in the cache.</summary>
        /// <remarks>The getter violates the lock contract on <see cref="unspents"/>, but the lock here is unnecessary as the <see cref="unspents"/> is marked as readonly.</remarks>
        public int CacheEntryCount { get { return this.unspents.Count; } }

        /// <summary>Task that handles persisting of unsaved changes to the underlaying coinview. Used for synchronization.</summary>
        private Task flushingTask = Task.CompletedTask;
        /// <summary>Task that handles rewinding of the the underlaying coinview. Used for synchronization.</summary>
        private Task rewindingTask = Task.CompletedTask;

        /// <summary>
        /// Initializes instance of the object based on DBreeze based coinview.
        /// </summary>
        /// <param name="inner">Underlaying coinview with database storage.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        public CachedCoinView(DBreezeCoinView inner, StakeChainStore stakeChainStore = null) :
            this(stakeChainStore)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <summary>
        /// Initializes instance of the object based on memory based coinview.
        /// </summary>
        /// <param name="inner">Underlaying coinview with memory based storage.</param>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        /// <remarks>
        /// This is used for testing the coinview.
        /// It allows a coin view that only has in-memory entries.
        /// </remarks>
        public CachedCoinView(InMemoryCoinView inner, StakeChainStore stakeChainStore = null) :
            this(stakeChainStore)
        {
            Guard.NotNull(inner, nameof(inner));
            this.inner = inner;
        }

        /// <summary>
        /// Initializes instance of the object based.
        /// </summary>
        /// <param name="stakeChainStore">Storage of POS block information.</param>
        private CachedCoinView(StakeChainStore stakeChainStore = null)
        {
            this.stakeChainStore = stakeChainStore;
            this.MaxItems = CacheMaxItemsDefault;
            this.lockobj = new ReaderWriterLock();
            this.unspents = new Dictionary<uint256, CacheItem>();
            this.PerformanceCounter = new CachePerformanceCounter();
        }

        /// <inheritdoc />
        public override async Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
        {
            Guard.NotNull(txIds, nameof(txIds));

            FetchCoinsResponse result = null;
            UnspentOutputs[] outputs = new UnspentOutputs[txIds.Length];
            List<int> miss = new List<int>();
            List<uint256> missedTxIds = new List<uint256>();
            using (this.lockobj.LockRead())
            {
                this.WaitOngoingTasks();
                for (int i = 0; i < txIds.Length; i++)
                {
                    CacheItem cache;
                    if (!this.unspents.TryGetValue(txIds[i], out cache))
                    {
                        miss.Add(i);
                        missedTxIds.Add(txIds[i]);
                    }
                    else
                    {
                        outputs[i] = cache.UnspentOutputs == null ? null :
                                     cache.UnspentOutputs.IsPrunable ? null :
                                     cache.UnspentOutputs.Clone();
                    }
                }

                this.PerformanceCounter.AddMissCount(miss.Count);
                this.PerformanceCounter.AddHitCount(txIds.Length - miss.Count);
            }

            FetchCoinsResponse fetchedCoins = await this.Inner.FetchCoinsAsync(missedTxIds.ToArray()).ConfigureAwait(false);
            using (this.lockobj.LockWrite())
            {
                this.flushingTask.Wait();
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
                    CacheItem cache = new CacheItem();
                    cache.ExistInInner = unspent != null;
                    cache.IsDirty = false;
                    cache.UnspentOutputs = unspent;
                    cache.OriginalOutputs = unspent?._Outputs.ToArray();
                    this.unspents.TryAdd(txIds[index], cache);
                }
                result = new FetchCoinsResponse(outputs, this.blockHash);
            }

            if (this.CacheEntryCount > this.MaxItems)
            {
                this.Evict();
                if (this.CacheEntryCount > this.MaxItems)
                {
                    await this.FlushAsync().ConfigureAwait(false);
                    this.Evict();
                }

            }

            return result;
        }

        /// <summary>
        /// Finds all changed records in the cache and persists them to the underlaying coinview.
        /// </summary>
        public async Task FlushAsync()
        {
            // Before flushing the coinview persist the stake store
            // the stake store depends on the last block hash
            // to be stored after the stake store is persisted.
            if (this.stakeChainStore != null)
                await this.stakeChainStore.Flush(true);

            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetBlockHashAsync().ConfigureAwait(false);

            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if (this.innerBlockHash == null)
                    return;

                KeyValuePair<uint256, CacheItem>[] unspent = this.unspents.Where(u => u.Value.IsDirty).ToArray();

                List<TxOut[]> originalOutputs = unspent.Select(u => u.Value.OriginalOutputs).ToList();
                foreach (KeyValuePair<uint256, CacheItem> u in unspent)
                {
                    u.Value.IsDirty = false;
                    u.Value.ExistInInner = true;
                    u.Value.OriginalOutputs = u.Value.UnspentOutputs?._Outputs.ToArray();
                }

                this.flushingTask = this.Inner.SaveChangesAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), originalOutputs, this.innerBlockHash, this.blockHash);

                // Remove prunable entries from cache as they are being flushed down.
                IEnumerable<KeyValuePair<uint256, CacheItem>> prunableEntries = unspent.Where(c => (c.Value.UnspentOutputs != null) && c.Value.UnspentOutputs.IsPrunable);
                foreach (KeyValuePair<uint256, CacheItem> entry in prunableEntries)
                    this.unspents.Remove(entry.Key);

                this.innerBlockHash = this.blockHash;
            }

            // Can't await inside a lock.
            await this.flushingTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes some items from the cache to free space for new items. 
        /// Only items that are persisted in the underlaying storage can be deleted from the cache.
        /// </summary>
        private void Evict()
        {
            using (this.lockobj.LockWrite())
            {
                // TODO: Do not create new random source every time.
                Random rand = new Random();
                foreach (KeyValuePair<uint256, CacheItem> entry in this.unspents.ToList())
                {
                    if (!entry.Value.IsDirty)
                    {
                        if (rand.Next() % 3 == 0)
                            this.unspents.Remove(entry.Key);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if ((this.blockHash != null) && (oldBlockHash != this.blockHash))
                    return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));

                this.blockHash = nextBlockHash;
                foreach (UnspentOutputs unspent in unspentOutputs)
                {
                    CacheItem existing;
                    if (this.unspents.TryGetValue(unspent.TransactionId, out existing))
                    {
                        if (existing.UnspentOutputs != null) existing.UnspentOutputs.Spend(unspent);
                        else existing.UnspentOutputs = unspent;
                    }
                    else
                    {
                        existing = new CacheItem();
                        existing.ExistInInner = !unspent.IsFull; // Seems to be a new created coin (careful, untrue if rewinding).
                        existing.ExistInInner |= duplicateTransactions.Any(t => unspent.TransactionId == t);
                        existing.IsDirty = true;
                        existing.UnspentOutputs = unspent;
                        this.unspents.Add(unspent.TransactionId, existing);
                    }
                    existing.IsDirty = true;
                    // Inner does not need to know pruned unspent that it never saw.
                    if (existing.UnspentOutputs.IsPrunable && !existing.ExistInInner)
                        this.unspents.Remove(unspent.TransactionId);
                }

                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public override async Task<uint256> Rewind()
        {
            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetBlockHashAsync().ConfigureAwait(false);

            Task<uint256> rewindingInner = null;
            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if (this.blockHash == this.innerBlockHash)
                    this.unspents.Clear();

                if (this.unspents.Count != 0)
                {
                    // More intelligent version can restore without throwing away the cache. (as the rewind data is in the cache).
                    this.unspents.Clear();
                    this.blockHash = this.innerBlockHash;
                    return this.blockHash;
                }
                else
                {
                    rewindingInner = this.inner.Rewind();
                    this.rewindingTask = rewindingInner;
                }
            }

            uint256 hash = await rewindingInner.ConfigureAwait(false);
            using (this.lockobj.LockWrite())
            {
                this.innerBlockHash = hash;
                this.blockHash = hash;
            }
            return hash;
        }

        /// <summary>
        /// Wait until flushing and rewinding task complete if any is in progress.
        /// </summary>
        /// <remarks>TODO: This is blocking call and is used in async methods, which quite 
        /// strongly erases the goals of using async in those methods.</remarks>
        private void WaitOngoingTasks()
        {
            Task.WaitAll(this.flushingTask, this.rewindingTask);
        }
    }
}