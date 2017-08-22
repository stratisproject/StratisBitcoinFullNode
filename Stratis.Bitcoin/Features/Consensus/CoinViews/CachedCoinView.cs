using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class CachedCoinView : CoinView, IBackedCoinView
    {
        class CacheItem
        {
            public UnspentOutputs UnspentOutputs;
            public bool ExistInInner;
            public bool IsDirty;
            public TxOut[] OriginalOutputs;
        }

        private readonly Stratis.Bitcoin.Utilities.ReaderWriterLock lockobj;
        private readonly Dictionary<uint256, CacheItem> unspents;
        private uint256 blockHash;
        private uint256 innerBlockHash;
        private readonly CoinView inner;
        private readonly StakeChainStore stakeChainStore;

        public CachedCoinView(DBreezeCoinView inner, StakeChainStore stakeChainStore = null)
        {
            Guard.NotNull(inner, nameof(inner));

            this.inner = inner;
            this.stakeChainStore = stakeChainStore;
            this.MaxItems = 100000;
            this.lockobj = new Stratis.Bitcoin.Utilities.ReaderWriterLock();
            this.unspents = new Dictionary<uint256, CacheItem>();
            this.PerformanceCounter = new CachePerformanceCounter();
        }

        /// <summary>
        /// This is used for testing the coin view
        /// it allows a coinview that only has in memory entries
        /// </summary>
        public CachedCoinView(InMemoryCoinView inner, StakeChainStore stakeChainStore = null)
        {
            Guard.NotNull(inner, nameof(inner));

            this.inner = inner;
            this.stakeChainStore = stakeChainStore;
            this.MaxItems = 100000;
            this.lockobj = new Stratis.Bitcoin.Utilities.ReaderWriterLock();
            this.unspents = new Dictionary<uint256, CacheItem>();
            this.PerformanceCounter = new CachePerformanceCounter();
        }

        public CoinView Inner => this.inner;

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
            var fetchedCoins = await this.Inner.FetchCoinsAsync(missedTxIds.ToArray()).ConfigureAwait(false);
            using (this.lockobj.LockWrite())
            {
                this.flushing.Wait();
                var innerblockHash = fetchedCoins.BlockHash;
                if (this.blockHash == null)
                {
                    Debug.Assert(this.unspents.Count == 0);
                    this.innerBlockHash = innerblockHash;
                    this.blockHash = this.innerBlockHash;
                }
                for (int i = 0; i < miss.Count; i++)
                {
                    var index = miss[i];
                    var unspent = fetchedCoins.UnspentOutputs[i];
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

        Task flushing = Task.CompletedTask;
        public async Task FlushAsync()
        {
            // before flushing the coinview persist the stake store
            // the stake store depends on the last block hash
            // to be stored after the stake store is persisted
            if (this.stakeChainStore != null)
                await this.stakeChainStore.Flush(true);

            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetBlockHashAsync().ConfigureAwait(false);

            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if (this.innerBlockHash == null)
                    return;
                var unspent =
                this.unspents.Where(u => u.Value.IsDirty)
                .ToArray();

                var originalOutputs = unspent.Select(u => u.Value.OriginalOutputs).ToList();
                foreach (var u in unspent)
                {
                    u.Value.IsDirty = false;
                    u.Value.ExistInInner = true;
                    u.Value.OriginalOutputs = u.Value.UnspentOutputs?._Outputs.ToArray();
                }
                this.flushing = this.Inner.SaveChangesAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), originalOutputs, this.innerBlockHash, this.blockHash);

                //Remove from cache prunable entries as they are being flushed down
                foreach (var c in unspent.Where(c => c.Value.UnspentOutputs != null && c.Value.UnspentOutputs.IsPrunable))
                    this.unspents.Remove(c.Key);
                this.innerBlockHash = this.blockHash;
            }
            //Can't await inside a lock
            await this.flushing.ConfigureAwait(false);
        }

        private void Evict()
        {
            using (this.lockobj.LockWrite())
            {
                Random rand = new Random();
                foreach (var entry in this.unspents.ToList())
                {
                    if (!entry.Value.IsDirty)
                    {
                        if (rand.Next() % 3 == 0)
                            this.unspents.Remove(entry.Key);
                    }
                }
            }
        }

        public int CacheEntryCount => this.unspents.Count;

        public int MaxItems
        {
            get; set;
        }

        public CachePerformanceCounter PerformanceCounter
        {
            get; set;
        }

        private static readonly uint256[] DuplicateTransactions = new[] { new uint256("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468"), new uint256("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599") };
        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if (this.blockHash != null && oldBlockHash != this.blockHash)
                    return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
                this.blockHash = nextBlockHash;
                foreach (var unspent in unspentOutputs)
                {
                    CacheItem existing;
                    if (this.unspents.TryGetValue(unspent.TransactionId, out existing))
                    {
                        if (existing.UnspentOutputs != null)
                            existing.UnspentOutputs.Spend(unspent);
                        else
                            existing.UnspentOutputs = unspent;
                    }
                    else
                    {
                        existing = new CacheItem();
                        existing.ExistInInner = !unspent.IsFull; //seems to be a new created coin (careful, untrue if rewinding)
                        existing.ExistInInner |= DuplicateTransactions.Any(t => unspent.TransactionId == t);
                        existing.IsDirty = true;
                        existing.UnspentOutputs = unspent;
                        this.unspents.Add(unspent.TransactionId, existing);
                    }
                    existing.IsDirty = true;
                    //Inner does not need to know pruned unspent that it never saw.
                    if (existing.UnspentOutputs.IsPrunable && !existing.ExistInInner)
                        this.unspents.Remove(unspent.TransactionId);
                }
                return Task.FromResult(true);
            }
        }

        Task rewinding = Task.CompletedTask;
        public override async Task<uint256> Rewind()
        {
            if (this.innerBlockHash == null)
                this.innerBlockHash = await this.inner.GetBlockHashAsync().ConfigureAwait(false);

            Task<uint256> rewindinginner = null;
            using (this.lockobj.LockWrite())
            {
                this.WaitOngoingTasks();
                if (this.blockHash == this.innerBlockHash)
                    this.unspents.Clear();
                if (this.unspents.Count != 0)
                {
                    //More intelligent version can restore without throwing away the cache. (as the rewind data is in the cache)
                    this.unspents.Clear();
                    this.blockHash = this.innerBlockHash;
                    return this.blockHash;
                }
                else
                {
                    rewindinginner = this.inner.Rewind();
                    this.rewinding = rewindinginner;
                }
            }
            var h = await rewindinginner.ConfigureAwait(false);
            using (this.lockobj.LockWrite())
            {
                this.innerBlockHash = h;
                this.blockHash = h;
            }
            return h;
        }

        private void WaitOngoingTasks()
        {
            Task.WaitAll(this.flushing, this.rewinding);
        }
    }
}