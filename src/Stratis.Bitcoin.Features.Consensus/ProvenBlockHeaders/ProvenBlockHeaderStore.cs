using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStore : IProvenBlockHeaderStore
    {
        /// <summary>
        /// Instance logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Allows consumers to perform clean-up during a graceful shutdown.
        /// </summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// Thread safe class representing a chain of headers from genesis.
        /// </summary>
        private readonly ConcurrentChain chain;

        /// <summary>
        /// Database repository storing <see cref="ProvenBlockHeader"></see> items.
        /// </summary>
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        /// <summary>
        /// Performance counter to measure performance of the save and get operations.
        /// </summary>
        private readonly BackendPerformanceCounter performanceCounter;

        /// <summary>
        /// Latest snapshot performance counter to measure performance of the save and get operations.
        /// </summary>
        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>
        /// Current block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Pending - not yet saved to disk - block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        private HashHeightPair pendingTipHashHeight;

        /// <summary>
        /// The async loop we wait upon.
        /// </summary>
        private IAsyncLoop asyncLoop;

        /// <summary>
        /// Factory for creating background async loop tasks.
        /// </summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>
        /// Limit <see cref="Cache"/> size to 100MB.
        /// </summary>
        private readonly int MemoryCacheSizeLimitInBytes = 100 * 1024 * 1024;

        /// <summary>
        /// Current size of the Cache in bytes.
        /// </summary>
        private long CacheSizeInBytes;

        /// <summary>
        /// Cache of pending <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Pending <see cref= "ProvenBlockHeader"/> items will be saved to disk every minute.
        /// </remarks>
        public ConcurrentDictionary<int, ProvenBlockHeader> PendingBatch { get; }

        /// <summary>
        /// Store Cache of <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Items are added to this cache when the caller asks for a <see cref= "ProvenBlockHeader"/>.
        /// </remarks>
        public ConcurrentDictionary<int, ProvenBlockHeader> Cache { get; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"></see> DBreeze repository.</param>
        /// <param name="nodeLifetime">Allows consumers to perform clean-up during a graceful shutdown.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        /// <param name="asyncLoopFactory">Factory for creating and also possibly starting application defined tasks inside async loop.</param>
        public ProvenBlockHeaderStore(
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeLifetime nodeLifetime,
            INodeStats nodeStats,
            IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(nodeStats, nameof(nodeStats));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chain = chain;
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;
            this.nodeLifetime = nodeLifetime;

            this.PendingBatch = new ConcurrentDictionary<int, ProvenBlockHeader>();
            this.Cache = new ConcurrentDictionary<int, ProvenBlockHeader>();

            this.asyncLoopFactory = asyncLoopFactory;

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;

            this.logger.LogDebug("Initialized ProvenBlockHader block tip at '{0}'.", this.TipHashHeight);

            this.asyncLoop = this.asyncLoopFactory.Run("ProvenBlockHeaders job", token =>
            {
                // Save pending items.
                this.SaveAsync().ConfigureAwait(false);

                // Check and make sure the cache limit hasn't been breached.
                this.ManangeCacheSize();

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");

                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(1),
            startAfter: TimeSpan.FromMinutes(1));
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            ProvenBlockHeader header = null;

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                header = this.CheckCache(blockHeight);

                if (header == null)
                {
                    // Check the repository (DBreeze).
                    header = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                    this.TryAddToCache(blockHeight, header);
                }
            }

            return header;
        }

        /// <inheritdoc />
        public async Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            var headersInCache = new List<ProvenBlockHeader>();

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                int heightKey = fromBlockHeight;

                ProvenBlockHeader header = null;

                var headersNotInCache = new List<int>();

                do
                {
                    header = this.CheckCache(heightKey);

                    if (header != null)
                        headersInCache.Add(header);
                    else
                        headersNotInCache.Add(heightKey);

                    heightKey++;

                } while (heightKey <= toBlockHeight);

                // Try and get items from the repository if not found in the store cache.
                if (headersNotInCache.Count > 0)
                {
                    var repositoryHeaders = new List<ProvenBlockHeader>();

                    foreach(int headerNotInCache in headersNotInCache)
                    {
                        var repositoryHeader = new ProvenBlockHeader();

                        repositoryHeader = await this.provenBlockHeaderRepository.GetAsync(headerNotInCache).ConfigureAwait(false);

                        if (repositoryHeader != null)
                        {
                            repositoryHeaders.Add(repositoryHeader);

                            this.TryAddToCache(headerNotInCache, repositoryHeader);
                        }
                    }

                    if (repositoryHeaders.Count > 0)
                        headersInCache.AddRange(repositoryHeaders);
                }
            }

            return headersInCache;
        }

        /// <inheritdoc />
        public void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip)
        {
            this.PendingBatch.AddOrUpdate(newTip.Height, provenBlockHeader, (key, value) => { return provenBlockHeader; });

            this.Cache.AddOrUpdate(newTip.Height, provenBlockHeader, (key, value) => { return provenBlockHeader; });

            this.pendingTipHashHeight = newTip;
        }

        /// <summary> Saves pending <see cref="ProvenBlockHeader"/> items to the <see cref="ProvenBlockHeaderRepository"/>.</summary>
        private async Task SaveAsync()
        {
            if (this.PendingBatch.Count == 0)
            {
                this.logger.LogTrace("(-)[PENDING_BATCH_EMPTY]");
                return;
            }

            if (!this.PendingBatch.Keys.OrderBy(s => s).SequenceEqual(this.PendingBatch.Keys))
            {
                this.logger.LogTrace("(-)[PENDING_BATCH_INCORRECT_SEQEUNCE]");
                throw new InvalidOperationException("Invalid ProvenBlockHeader pending batch sequence - unable to save to the database repository.");
            }

            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                var pendingBatchKeys = this.PendingBatch.Keys.ToList();

                List<ProvenBlockHeader> pendingBatchValues = this.PendingBatch.Values.ToList();

                await this.provenBlockHeaderRepository.PutAsync(
                    pendingBatchValues, this.pendingTipHashHeight).ConfigureAwait(false);

                pendingBatchKeys.ForEach(
                    blockHeight => this.PendingBatch.TryRemove(blockHeight, out ProvenBlockHeader unused));

                this.pendingTipHashHeight = null;
            }
        }

        /// <summary>
        /// Gets <see cref="ProvenBlockHeader"></see> items from cache with specific key if present.
        /// </summary>
        /// <param name="key"> Item's key.</param>
        /// <param name = "items"> Dictionary of items in cache</param>
        /// <returns> <see cref="ProvenBlockHeader"></see> if cache contains the item, <c>null</c> otherwise.</returns>
        private ProvenBlockHeader GetHeaderFromCache(int key, ConcurrentDictionary<int, ProvenBlockHeader> items)
        {
            if (items.TryGetValue(key, out ProvenBlockHeader header))
            {
                this.logger.LogTrace("(-):{0}", header);

                return header;
            }

            return null;
        }

        /// <summary>
        /// Manages store cache size. Remove 30% of items if the memory cache size has been reached.
        /// </summary>
        private void ManangeCacheSize()
        {
            do
            {
                if (this.CacheSizeInBytes > this.MemoryCacheSizeLimitInBytes)
                {
                    List<int> sortedList = this.Cache.Keys.OrderBy(s => s).ToList();

                    var itemsToRemove = sortedList.Take(Convert.ToInt32(sortedList.Count * 0.3));

                    foreach (var itemToRemove in itemsToRemove)
                    {
                        if (this.Cache.TryRemove(itemToRemove, out ProvenBlockHeader header))
                            this.CacheSizeInBytes -= header.HeaderSize;
                    }
                }

            } while (this.CacheSizeInBytes > this.MemoryCacheSizeLimitInBytes);
        }

        /// <summary>
        /// Check pending cache first. Then check store cache next.
        /// </summary>
        /// <param name="blockHeight">Item's key.</param>
        /// <returns><see cref="ProvenBlockHeader"> if found, otherwise null.</returns>
        private ProvenBlockHeader CheckCache(int blockHeight)
        {
            ProvenBlockHeader header = null;

            header = this.GetHeaderFromCache(blockHeight, this.PendingBatch);

            if (header == null)
                header = this.GetHeaderFromCache(blockHeight, this.Cache);

            return header;
        }

        /// <summary>
        /// Adds <see cref="ProvenBlockHeader"> items to cache and update the size of the cache in bytes.
        /// </summary>
        /// <param name="blockHeight">Block height key.</param>
        /// <param name="header"><see cref="ProvenBlockHeader"> to add.</param>
        private void AddToCache(int blockHeight, ProvenBlockHeader header)
        {
            this.Cache.AddOrUpdate(blockHeight, header, (key, value) => { return header; });

            this.CacheSizeInBytes += header.HeaderSize;
        }

        /// <summary> Only add to cache if the item does not already exist.</summary>
        /// <param name="blockHeight">Block height key.</param>
        /// <param name="header"><see cref="ProvenBlockHeader"> to add.</param>///
        private void TryAddToCache(int blockHeight, ProvenBlockHeader header)
        {
            if (!this.Cache.TryGetValue(blockHeight, out ProvenBlockHeader cachedHeader))
                this.AddToCache(blockHeight, header);
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                benchLog.AppendLine(snapShot.ToString());
            else
                benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}