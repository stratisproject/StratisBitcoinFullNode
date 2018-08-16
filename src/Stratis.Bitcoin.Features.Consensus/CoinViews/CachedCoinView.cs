using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Cache layer for coinview prevents too frequent updates of the data in the underlying storage.
    /// </summary>
    public class CachedCoinView : ICachedCoinView, IBackedCoinView
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
        /// Rewind data with its byte size that will be kept in the queue until it is persisted to a storage.
        /// </summary>
        private class QueuedRewindData
        {
            /// <summary>Rewind data to be saved.</summary>
            public RewindData RewindData { get; set; }

            /// <summary>The byte size of items in rewind data object.</summary>
            public long DataSizeInBytes => (this.RewindData.PreviousBlockHash.Size) +
                                           (this.RewindData.TransactionsToRemove?.Sum(t => t.Size) ?? 0) +
                                           (this.RewindData.OutputsToRestore?.Sum(o => o.GetSizeInBytes()) ?? 0);
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
        public const int CacheMaxItemsDefault = 100_000;

        /// <summary>The current batch size in bytes.</summary>
        private long currentBatchSizeBytes;

        /// <summary>Maximum interval between saving batches.</summary>
        /// <remarks>Interval value is a prime number that wasn't used as an interval in any other component. That prevents having CPU consumption spikes.</remarks>
        private const int BatchMaxSaveIntervalSeconds = 41;

        /// <summary>Maximum number of bytes the batch can hold until the rewind data items are stored to the disk.</summary>
        internal const int BatchThresholdSizeBytes = 5 * 1_000 * 1_000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Queue which contains rewind data items that should be saved to the database.</summary>
        private readonly AsyncQueue<QueuedRewindData> rewindDataQueue;

        /// <summary>Batch of rewind data items which should be saved in the database.</summary>
        private readonly List<QueuedRewindData> rewindDataBatch;

        /// <summary>Task that runs <see cref="DequeueRewindDataContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        /// <summary>Maximum number of transactions in the cache.</summary>
        public int MaxItems { get; set; }

        /// <summary>Statistics of hits and misses in the cache.</summary>
        public CachePerformanceCounter PerformanceCounter { get; set; }

        /// <summary>Lock object to protect access to <see cref="unspents"/>, <see cref="blockHash"/>, and <see cref="persistedBlockHash"/>.</summary>
        private readonly AsyncLock lockobj;

        /// <summary>Hash of the block headers of the tip of the coinview.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 blockHash;

        /// <summary>Hash of the block headers of the tip of the underlaying coinview storage.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private uint256 persistedBlockHash;

        /// <summary>Coin view at one layer below this implementaiton.</summary>
        public ICoinViewStorage CoinViewStorage { get; }

        private readonly INodeLifetime nodeLifetime;

        /// <summary>Information about cached items mapped by transaction IDs the cached item's unspent outputs belong to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockobj"/>.</remarks>
        private readonly Dictionary<uint256, CacheItem> unspents;

        /// <summary>Number of items in the cache.</summary>
        /// <remarks>The getter violates the lock contract on <see cref="unspents"/>, but the lock here is unnecessary as the <see cref="unspents"/> is marked as readonly.</remarks>
        public int CacheEntryCount => this.unspents.Count;

        private readonly IChainState chainState;

        /// <summary>
        /// Initializes instance of the object based on DBreeze based coinview.
        /// </summary>
        /// <param name="chainState">Chain state.</param>
        /// <param name="coinViewStorage">Underlying coinview with database storage.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeLifetime">Node life time management object.</param>
        public CachedCoinView(
            IChainState chainState,
            ICoinViewStorage coinViewStorage,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime
        ) : this(chainState, dateTimeProvider, loggerFactory, nodeLifetime)
        {
            Guard.NotNull(coinViewStorage, nameof(coinViewStorage));
            this.CoinViewStorage = coinViewStorage;
        }

        /// <summary>
        /// Initializes instance of the object based.
        /// </summary>
        /// <param name="chainState">Chain state.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeLifetime">Node life time management object.</param>
        private CachedCoinView(
            IChainState chainState,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.MaxItems = CacheMaxItemsDefault;
            this.lockobj = new AsyncLock();
            this.unspents = new Dictionary<uint256, CacheItem>();
            this.PerformanceCounter = new CachePerformanceCounter(dateTimeProvider);
            this.rewindDataBatch = new List<QueuedRewindData>();
            this.rewindDataQueue = new AsyncQueue<QueuedRewindData>();
        }

        public void Initialize()
        {
            this.dequeueLoopTask = this.DequeueRewindDataContinuouslyAsync();
        }

        /// <inheritdoc />
        public async Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            if (this.blockHash == null)
            {
                FetchCoinsResponse response = await this.FetchCoinsAsync(new uint256[0], cancellationToken).ConfigureAwait(false);

                this.persistedBlockHash = response.BlockHash;
                this.blockHash = this.persistedBlockHash;
            }

            this.logger.LogTrace("(-):'{0}'", this.blockHash);
            return this.blockHash;
        }

        /// <inheritdoc />
        public async Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.NotNull(txIds, nameof(txIds));
            this.logger.LogTrace("({0}.{1}:{2})", nameof(txIds), nameof(txIds.Length), txIds.Length);

            FetchCoinsResponse result;
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
            FetchCoinsResponse fetchedCoins = await this.CoinViewStorage.FetchCoinsAsync(missedTxIds.ToArray(), cancellationToken).ConfigureAwait(false);

            using (await this.lockobj.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                uint256 innerblockHash = fetchedCoins.BlockHash;
                if (this.blockHash == null)
                {
                    Debug.Assert(this.unspents.Count == 0);
                    this.persistedBlockHash = innerblockHash;
                    this.blockHash = this.persistedBlockHash;
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
        public async Task AddRewindDataAsync(IList<UnspentOutputs> unspentOutputs, ChainedHeader currentBlock)
        {
            Guard.NotNull(currentBlock, nameof(currentBlock));
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            this.logger.LogTrace("({0}.Count():{1},{2}:'{3}')", nameof(unspentOutputs), unspentOutputs.Count, nameof(currentBlock), currentBlock.HashBlock);

            RewindData rewindData = new RewindData(currentBlock.HashBlock);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if ((this.blockHash != null) && (currentBlock.Previous.HashBlock != this.blockHash))
                {
                    this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                    throw new InvalidOperationException("Invalid oldBlockHash");
                }

                this.blockHash = currentBlock.HashBlock;
                foreach (UnspentOutputs unspent in unspentOutputs)
                {
                    CacheItem existing;
                    if (this.unspents.TryGetValue(unspent.TransactionId, out existing))
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' are in cache already, updating them.", unspent.TransactionId);
                        if (existing.UnspentOutputs != null) existing.UnspentOutputs.Spend(unspent);
                        else existing.UnspentOutputs = unspent;
                    }
                    else
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' not found in cache, inserting them.", unspent.TransactionId);
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
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' are prunable and not in underlaying coinview, removing from cache.", unspent.TransactionId);
                        this.unspents.Remove(unspent.TransactionId);
                    }

                    this.unspents.TryGetValue(unspent.TransactionId, out CacheItem original);
                    if (original == null)
                    {
                        // This one haven't existed before, if we rewind, delete it.
                        rewindData.TransactionsToRemove.Add(unspent.TransactionId);
                    }
                    else
                    {
                        // We'll need to restore the original outputs.
                        UnspentOutputs clone = unspent.Clone();
                        clone.Outputs = original.UnspentOutputs.Outputs;
                        rewindData.OutputsToRestore.Add(clone);
                    }
                }

                this.rewindDataQueue.Enqueue(new QueuedRewindData { RewindData = rewindData });
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task<uint256> Rewind()
        {
            this.logger.LogTrace("()");

            if (this.persistedBlockHash == null)
                this.persistedBlockHash = await this.CoinViewStorage.GetTipHashAsync().ConfigureAwait(false);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if (this.rewindDataBatch.Any())
                {
                    QueuedRewindData lastItem = this.rewindDataBatch.Last();
                    foreach (uint256 transactionToRemove in lastItem.RewindData.TransactionsToRemove)
                    {
                        if (!this.unspents.ContainsKey(transactionToRemove)) continue;
                        this.unspents.Remove(transactionToRemove);
                    }

                    this.rewindDataBatch.Remove(lastItem);

                    this.logger.LogTrace("(-)[REMOVED_FROM_BATCH]:'{0}'", lastItem.RewindData.PreviousBlockHash);
                    return lastItem.RewindData.PreviousBlockHash;
                } 

                uint256 hash = await this.CoinViewStorage.Rewind().ConfigureAwait(false);
                this.persistedBlockHash = hash;
                this.blockHash = hash;

                this.logger.LogTrace("(-):'{0}'", hash);
                return hash;
            }
        }

        /// <summary>
        /// Dequeues the rewind data continuously and saves it to the database when max batch size is reached or timer ran out.
        /// </summary>
        /// <remarks>Batch is always saved on shutdown.</remarks>
        private async Task DequeueRewindDataContinuouslyAsync()
        {
            this.logger.LogTrace("()");

            Task<QueuedRewindData> dequeueTask = null;
            Task timerTask = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Start new dequeue task if not started already.
                dequeueTask = dequeueTask ?? this.rewindDataQueue.DequeueAsync();

                // Wait for one of the tasks: dequeue or timer (if available) to finish.
                Task task = (timerTask == null) ? dequeueTask : await Task.WhenAny(dequeueTask, timerTask).ConfigureAwait(false);

                bool saveBatch = false;

                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Happens when node is shutting down or Dispose() is called.
                    // We want to save whatever is in the batch before exiting the loop.
                    saveBatch = true;

                    this.logger.LogDebug("Node is shutting down. Save batch.");
                }

                // Save batch if timer ran out or we've dequeued a new rewind data 
                // or the max batch size is reached or the node is shutting down.
                if (dequeueTask.Status == TaskStatus.RanToCompletion)
                {
                    QueuedRewindData item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    this.rewindDataBatch.Add(item);

                    this.currentBatchSizeBytes += item.DataSizeInBytes;

                    saveBatch = saveBatch || (this.currentBatchSizeBytes >= BatchThresholdSizeBytes) || this.chainState.IsAtBestChainTip;
                }
                else
                {
                    // Will be executed in case timer ran out or node is being shut down.
                    saveBatch = true;
                }

                if (saveBatch)
                {
                    if (this.rewindDataBatch.Count != 0)
                    {
                        await this.SaveBatchAsync().ConfigureAwait(false);

                        this.rewindDataBatch.Clear();

                        this.currentBatchSizeBytes = 0;
                    }

                    timerTask = null;
                }
                else
                {
                    // Start timer if it is not started already.
                    timerTask = timerTask ?? Task.Delay(BatchMaxSaveIntervalSeconds * 1000, this.nodeLifetime.ApplicationStopping);
                }
            }

            if (this.rewindDataBatch.Count != 0)
                await this.SaveBatchAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Saves batch to a persistent storage.
        /// </summary>
        private async Task SaveBatchAsync()
        {
            this.logger.LogTrace("()");

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if (this.persistedBlockHash == null)
                {
                    this.logger.LogTrace("(-)[NULL_INNER_TIP]");
                    return;
                }

                KeyValuePair<uint256, CacheItem>[] unspent = this.unspents.Where(u => u.Value.IsDirty).ToArray();

                foreach (KeyValuePair<uint256, CacheItem> u in unspent)
                {
                    u.Value.IsDirty = false;
                    u.Value.ExistInInner = true;
                    u.Value.OriginalOutputs = u.Value.UnspentOutputs?.Outputs.ToArray();
                }

                // Update signature to only store rewind data
                await this.CoinViewStorage.PersistDataAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), this.rewindDataBatch.Select(r => r.RewindData).ToList(), this.persistedBlockHash, this.blockHash).ConfigureAwait(false);

                // Remove prunable entries from cache as they were flushed down.
                IEnumerable<KeyValuePair<uint256, CacheItem>> prunableEntries = unspent.Where(c => (c.Value.UnspentOutputs != null) && c.Value.UnspentOutputs.IsPrunable);
                foreach (KeyValuePair<uint256, CacheItem> entry in prunableEntries)
                    this.unspents.Remove(entry.Key);

                this.persistedBlockHash = this.blockHash;
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();

            // Let current batch saving task finish.
            this.rewindDataQueue.Dispose();
            this.dequeueLoopTask?.GetAwaiter().GetResult();
        }
    }
}
