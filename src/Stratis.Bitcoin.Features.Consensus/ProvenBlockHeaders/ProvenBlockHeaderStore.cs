using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStore : IProvenBlockHeaderStore, IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Allows consumers to perform clean-up during a graceful shutdown.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Chain state holds various information related to the status of the chain and its validation.</summary>
        private readonly IChainState chainState;

        /// <summary>Thread safe class representing a chain of headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Database repository storing <see cref="ProvenBlockHeader"></see>s.</summary>
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        /// <summary>Lock object to protect access to <see cref="ProvenBlockHeader"/>.</summary>
        private readonly AsyncLock lockobj;

        /// <summary>Performance counter to measure performance of the save and query operation.</summary>
        private readonly BackendPerformanceCounter performanceCounter;

        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>Current block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.</summary>
        public HashHeightPair CurrentTipHashHeight { get; private set; }

        /// <summary>Pending - not yet saved to disk - block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.</summary>
        private HashHeightPair pendingTipHashHeight;

        /// <summary>The async loop we wait upon.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Maximum number of items to cache.</summary>
        private const int CacheMaxItemsCount = 5_000;

        /// <summary>Limit <see cref="Cache"/> size to 100MB.</summary>
        private readonly int MaxMemoryCacheSizeInBytes = 100 * 1024 * 1024;

        /// <summary>Cache of pending <see cref= "ProvenBlockHeader"/> items.</summary>
        /// <remarks>Pending <see cref= "ProvenBlockHeader"/> items will be saved to disk every minute.</remarks>
        public ConcurrentDictionary<int, ProvenBlockHeader> PendingCache { get; }

        /// <summary>Store Cache of <see cref= "ProvenBlockHeader"/> items.</summary>
        /// <remarks>Items are added to this cache when the caller asks for a <see cref= "ProvenBlockHeader"/>.</remarks>
        public MemoryCache<int, ProvenBlockHeader> Cache { get; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"></see> DBreeze repository.</param>
        /// <param name="nodeLifetime">Allows consumers to perform clean-up during a graceful shutdown.</param>
        /// <param name="chainState">Chain state holds various information related to the status of the chain and its validation.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        /// <param name="asyncLoopFactory">Factory for creating and also possibly starting application defined tasks inside async loop.</param>
        public ProvenBlockHeaderStore(
            ConcurrentChain chain, 
            IDateTimeProvider dateTimeProvider, 
            ILoggerFactory loggerFactory, 
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeLifetime nodeLifetime,
            IChainState chainState,
            INodeStats nodeStats,
            IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(nodeStats, nameof(nodeStats));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chain = chain;
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;
            this.nodeLifetime = nodeLifetime;
            this.chainState = chainState;
            this.lockobj = new AsyncLock();

            this.PendingCache = new ConcurrentDictionary<int, ProvenBlockHeader>();
            this.Cache = new MemoryCache<int, ProvenBlockHeader>(CacheMaxItemsCount);

            this.asyncLoopFactory = asyncLoopFactory;

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing {0}.", nameof(ProvenBlockHeaderStore));

            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            this.CurrentTipHashHeight = await this.CurrentTipHashHeightAsync().ConfigureAwait(false);

            this.logger.LogDebug("Initialized ProvenBlockHader block tip at '{0}'.", this.CurrentTipHashHeight);

            this.asyncLoop = this.asyncLoopFactory.Run("ProvenBlockHeaders job", token =>
            {
                this.logger.LogTrace("()");

                // Save pending items.
                this.SaveAsync().ConfigureAwait(false);

                // Check and make sure the store cache limit isn't breached.
                this.ManangeCacheSize();

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");

                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(1),
            startAfter: TimeSpan.FromMinutes(1));

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHeight), blockHeight);

            ProvenBlockHeader header = null;

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                // Check pending cache first.
                header = this.GetHeaderFromPendingCache(blockHeight);

                if (header == null)
                {
                    // Check store cache next.
                    header = this.GetHeaderFromStoreCache(blockHeight);

                    if (header == null)
                    {
                        // Check the repository (DBreeze).
                        header = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                        // Add the item to the store cache.
                        this.Cache.AddOrUpdate(blockHeight, header);
                    }
                }
            }

            if (header != null)
                this.logger.LogTrace("(-):*.{0}='{1}'", nameof(header), header);
            else
                this.logger.LogTrace("(-):null");

            return header;
        }

        /// <inheritdoc />
        public async Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(fromBlockHeight), fromBlockHeight);
            this.logger.LogTrace("({0}:'{1}')", nameof(toBlockHeight), toBlockHeight);

            var cachedHeaders = new List<ProvenBlockHeader>();            

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                int heightKey = fromBlockHeight;

                // Check if the items exist in the store cache before checking the repository.
                ProvenBlockHeader headerCache = null;

                var blockHeightList = new List<int>();

                do
                {
                    headerCache = this.GetHeaderFromStoreCache(heightKey);

                    if (headerCache != null)
                        cachedHeaders.Add(headerCache);
                    else
                        blockHeightList.Add(heightKey);

                    heightKey++;

                } while (heightKey <= toBlockHeight);

                // Try and get items from the repository if not found in the store cache.
                if (blockHeightList.Count > 0)
                {
                    var repositoryHeaders = new List<ProvenBlockHeader>();

                    foreach(int blockHeight in blockHeightList)
                    {
                        var repositoryHeader = new ProvenBlockHeader();

                        repositoryHeader = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                        if (repositoryHeader != null)
                        {
                            repositoryHeaders.Add(repositoryHeader);

                            // When found also add to cache.
                            this.Cache.AddOrUpdate(blockHeight, repositoryHeader);
                        }
                    }

                    if (repositoryHeaders.Count > 0)
                        cachedHeaders.AddRange(repositoryHeaders);
                }
            }

            return cachedHeaders;
        }

        /// <inheritdoc />
        public async Task<HashHeightPair> CurrentTipHashHeightAsync()
        {
            this.logger.LogTrace("()");

            this.CurrentTipHashHeight = await this.provenBlockHeaderRepository.GetTipHashHeightAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");

            return this.CurrentTipHashHeight;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetTipAsync()
        {
            this.logger.LogTrace("()");

            this.CurrentTipHashHeight = await this.CurrentTipHashHeightAsync();

            this.logger.LogTrace("(-)");

            return await this.GetAsync(this.CurrentTipHashHeight.Height).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void AddToPending(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(provenBlockHeader), provenBlockHeader);

            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            this.PendingCache.AddOrUpdate(newTip.Height, provenBlockHeader, (key, value) => { return provenBlockHeader; });

            this.pendingTipHashHeight = newTip;

            this.logger.LogTrace("(-)");
        }

        /// <summary>Saves pending <see cref="ProvenBlockHeader"/> items to the <see cref="ProvenBlockHeaderRepository"/>.</summary>
        private async Task SaveAsync()
        {
            this.logger.LogTrace("()");

            if (this.PendingCache.Count == 0)
            {
                this.logger.LogTrace("(-)[PROVEN_BLOCK_HEADER_CACHE_EMPTY]");
                return;
            }

            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                using (await this.lockobj.LockAsync().ConfigureAwait(false))
                {
                    var pendingHeaders = new List<ProvenBlockHeader>();

                    for (int i = this.CurrentTipHashHeight.Height; i <= this.pendingTipHashHeight.Height; i++)
                    {
                        ProvenBlockHeader cachedHeader = this.GetHeaderFromPendingCache(i);

                        if (cachedHeader != null)
                        {
                            pendingHeaders.Add(cachedHeader);
                            this.PendingCache.TryRemove(i, out ProvenBlockHeader removedItem);
                        }
                    }

                    if (pendingHeaders.Count > 0)
                        await this.provenBlockHeaderRepository.PutAsync(pendingHeaders, this.pendingTipHashHeight).ConfigureAwait(false);

                    if (this.PendingCache.Count == 0)
                        this.pendingTipHashHeight = null;
                }
            }           

            this.logger.LogTrace("(-)");
        }

        /// <summary>Gets <see cref="ProvenBlockHeader"></see> items from pending cache with specific key if present.</summary>
        /// <param name="key">Item's key.</param>
        /// <returns><see cref="ProvenBlockHeader"></see> if cache contains the item, <c>null</c> otherwise.</returns>
        private ProvenBlockHeader GetHeaderFromPendingCache(int key)
        {
            this.logger.LogTrace("()");

            if (this.PendingCache.TryGetValue(key, out ProvenBlockHeader header))
            {
                this.logger.LogTrace("(-):{0}", header);

                return header;
            }

            this.logger.LogTrace("(-)");

            return null;
        }

        /// <summary>Gets <see cref="ProvenBlockHeader"></see> items from store cache with specific key if present.</summary>
        /// <param name="key">Item's key.</param>
        /// <returns><see cref="ProvenBlockHeader"></see> if cache contains the item, <c>null</c> otherwise.</returns>
        private ProvenBlockHeader GetHeaderFromStoreCache(int key)
        {
            this.logger.LogTrace("()");

            if (this.Cache.TryGetValue(key, out ProvenBlockHeader header))
            {
                this.logger.LogTrace("(-):{0}", header);

                return header;
            }

            this.logger.LogTrace("(-)");

            return null;
        }

        /// <summary>Manages store cache size.  Remove 30% of items if the memory cache size has been reached.</summary>
        private void ManangeCacheSize()
        {
            this.logger.LogTrace("()");

            do
            {
                if (this.MemoryCacheSize() > this.MaxMemoryCacheSizeInBytes)
                {
                    double removeThreshold = this.Cache.Count * 0.3;

                    int removedItemCount = 0;

                    for (int i = this.Cache.Count; i >= 0; i--)
                    {
                        if (this.Cache.TryGetValue(i, out ProvenBlockHeader header))
                        {
                            this.Cache.Remove(i);
                            removedItemCount++;

                            if (removedItemCount > removeThreshold)
                                break;
                        }
                    }
                }

            } while (this.MemoryCacheSize() > this.MaxMemoryCacheSizeInBytes);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Get current store cache size in bytes.</summary>
        /// <returns>Size of store cache in bytes.</returns>
        private long MemoryCacheSize()
        {
            this.logger.LogTrace("()");

            long size = 0;

            for (int i = 0; i <= this.Cache.Count - 1; i++)
            {
                if (this.Cache.TryGetValue(i, out ProvenBlockHeader header))
                    size += header.HeaderSize;
            }

            this.logger.LogTrace("(-)");

            return size;
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            this.logger.LogTrace("()");

            benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                benchLog.AppendLine(snapShot.ToString());
            else
                benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.lockobj.Dispose();

            this.asyncLoop?.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}