using System;
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
    /// <summary>
    /// Manages the persistence of <see cref="ProvenBlockHeader"/> items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pending batch is saved to the database and cleared every minute.
    /// </para>
    /// <para>
    /// Items in the pending batch are also saved to the least recently used <see cref="MemorySizeCache"/>.  This cache has a memory size limit of 100MB (see <see cref="MemoryCacheSizeLimitInBytes"/>).
    /// </para>
    /// <para>
    /// When new <see cref="ProvenBlockHeader"/> items are saved to the database, in case <see cref="IProvenBlockHeaderRepository"/> contains headers that
    /// are no longer a part of the best chain, they are overwritten or ignored.
    /// </para>
    /// <para>
    /// When <see cref="IProvenBlockHeaderStore"/> is being initialized we overwrite blocks that are not on the best chain.
    /// </para>
    /// </remarks>
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
        /// Database repository storing <see cref="ProvenBlockHeader"/> items.
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
        /// Current <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// The highest stored <see cref= "ProvenBlockHeader"/> in the repository.
        /// </summary>
        private ChainedHeader storeTip;

        /// <summary>
        /// Pending - not yet saved to disk - <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        /// <para>
        /// All access to these items have to be protected by <see cref="lockObject"/>.
        /// </para>
        private HashHeightPair pendingTipHashHeight;

        /// <summary>
        /// The async loop we wait upon.
        /// </summary>
        private IAsyncLoop asyncLoop;

        /// <summary>
        /// A lock object that protects access to the <see cref="PendingBatch"/> and <see cref="pendingTipHashHeight"/>.
        /// </summary>
        private readonly object lockObject;

        /// <summary>
        /// Factory for creating background async loop tasks.
        /// </summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>
        /// Limit <see cref="Cache"/> size to 100MB.
        /// </summary>
        private readonly long MemoryCacheSizeLimitInBytes = 100 * 1024 * 1024;

        /// <summary>
        /// Cache of pending <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Pending <see cref= "ProvenBlockHeader"/> items will be saved to disk every minute.
        /// <para>
        /// All access to these items have to be protected by <see cref="lockObject"/>.
        /// </para>
        /// </remarks>
        public readonly Dictionary<int, ProvenBlockHeader> PendingBatch;

        /// <summary>
        /// Store Cache of <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Items are added to this cache when the caller asks for a <see cref= "ProvenBlockHeader"/>.
        /// </remarks>
        public MemorySizeCache<int, ProvenBlockHeader> Cache { get; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"/> DBreeze repository.</param>
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

            this.lockObject = new object();
            this.PendingBatch = new Dictionary<int, ProvenBlockHeader>();
            this.Cache = new MemorySizeCache<int, ProvenBlockHeader>(this.MemoryCacheSizeLimitInBytes);

            this.asyncLoopFactory = asyncLoopFactory;

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <summary>
        /// Initializes the <see cref="ProvenBlockHeaderStore"/>.
        /// <para>
        /// If <see cref="storeTip"/> is <c>null</c>, the store is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>The node crashed.</item>
        ///     <item>The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover we walk back the <see cref= "ConcurrentChain "/> until a common <see cref= "HashHeightPair"/> is found and then set the <see cref="ProvenBlockHeaderStore"/>'s <see cref="storeTip"/> to that.
        /// </para>
        /// </summary>
        public async Task InitializeAsync()
        {
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            this.storeTip = this.chain.GetBlock(this.provenBlockHeaderRepository.TipHashHeight.Hash);

            if (this.storeTip == null)
            {
                this.storeTip = await this.RecoverStoreTipAsync().ConfigureAwait(false);
            }

            this.TipHashHeight = new HashHeightPair(this.storeTip);

            this.logger.LogDebug("Initialized ProvenBlockHeader store tip at '{0}'.", this.storeTip);

            this.asyncLoop = this.asyncLoopFactory.Run("ProvenBlockHeaders job", async token =>
            {
                await this.SaveAsync().ConfigureAwait(false);

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");
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
                this.Cache.TryGetValue(blockHeight, out header);

                if (header == null)
                {
                    // Check the repository.
                    header = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                    this.Cache.AddOrUpdate(blockHeight, header, header.HeaderSize);
                }
            }

            return header;
        }

        /// <inheritdoc />
        public async Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            Guard.Assert(toBlockHeight >= fromBlockHeight);

            var headersInCache = new List<ProvenBlockHeader>();

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                int index = fromBlockHeight;

                ProvenBlockHeader header = null;

                var headersNotInCache = new List<int>();

                do
                {
                    this.Cache.TryGetValue(index, out header);

                    if (header != null)
                    {
                        headersInCache.Add(header);
                    }
                    else
                    {
                        headersNotInCache.Add(index);
                    }

                    index++;

                } while (index <= toBlockHeight);

                // Try and get items from the repository if not found in the store cache.

                if (headersNotInCache.Count > 0)
                {
                    var repositoryHeaders = new List<ProvenBlockHeader>();

                    // Check the full range first
                    if ((headersNotInCache.Count - 1) == toBlockHeight - fromBlockHeight)
                    {
                        List<ProvenBlockHeader> rangeHeaders = await this.provenBlockHeaderRepository.GetAsync(fromBlockHeight, toBlockHeight);

                        index = fromBlockHeight;

                        foreach(ProvenBlockHeader rangeHeader in rangeHeaders)
                        {
                            if (rangeHeader != null)
                            {
                                repositoryHeaders.Add(rangeHeader);
                                this.Cache.AddOrUpdate(index, rangeHeader, rangeHeader.HeaderSize);
                            }

                            index++;
                        }
                    }
                    else
                    {
                        // If not a full sequence then check individually.
                        foreach (int headerNotInCache in headersNotInCache)
                        {
                            ProvenBlockHeader repositoryHeader = await this.provenBlockHeaderRepository.GetAsync(headerNotInCache).ConfigureAwait(false);

                            if (repositoryHeader != null)
                            {
                                repositoryHeaders.Add(repositoryHeader);

                                this.Cache.AddOrUpdate(headerNotInCache, repositoryHeader, repositoryHeader.HeaderSize);
                            }
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
            lock(this.lockObject)
            {
                this.PendingBatch.Add(newTip.Height, provenBlockHeader);

                this.pendingTipHashHeight = newTip;
            }

            this.Cache.AddOrUpdate(newTip.Height, provenBlockHeader, provenBlockHeader.HeaderSize);
        }

        /// <summary>
        /// Saves pending <see cref="ProvenBlockHeader"/> items to the <see cref="IProvenBlockHeaderRepository"/>, then removes them from the pending batch.
        /// </summary>
        private async Task SaveAsync()
        {
            if (this.pendingTipHashHeight == null)
                return;

            var pendingBatch = new List<KeyValuePair<int, ProvenBlockHeader>>();

            HashHeightPair hashHeight = null;

            lock (this.lockObject)
            {
                pendingBatch = this.PendingBatch.ToList();

                this.PendingBatch.Clear();

                hashHeight = this.pendingTipHashHeight;

                this.pendingTipHashHeight = null;
            }

            // Make sure the proven block header height keys, within the pendingBatch, are in sequence.
            List<int> sortedHeaderHeights = pendingBatch.OrderBy(s => s.Key).Select(s => s.Key).ToList();

            if (!sortedHeaderHeights.SequenceEqual(pendingBatch.Select(s => s.Key)))
            {
                this.logger.LogTrace("(-)[PENDING_BATCH_INCORRECT_SEQEUNCE]");

                throw new ProvenHeaderStoreException("Invalid ProvenBlockHeader pending batch sequence - unable to save to the database repository.");
            }

            // Save the items to disk.
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                await this.provenBlockHeaderRepository.PutAsync(
                    pendingBatch.Select(items => items.Value).ToList(), hashHeight).ConfigureAwait(false);

                this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;
            }
        }

        /// <summary>
        /// Will set the <see cref="IProvenBlockHeaderStore"/> tip to the last <see cref="ProvenBlockHeader"/> that exists both in the repository and in the <see cref="ConcurrentChain"/>.
        /// </summary>
        private async Task<ChainedHeader> RecoverStoreTipAsync()
        {
            uint256 latestBlockHash = this.provenBlockHeaderRepository.TipHashHeight.Hash;

            int tipHeight = this.provenBlockHeaderRepository.TipHashHeight.Height;

            ProvenBlockHeader latestHeader = await this.provenBlockHeaderRepository.GetAsync(tipHeight);

            if (latestHeader == null)
            {
                // Happens when the proven header store is corrupt.
                throw new ProvenHeaderStoreException("Proven block header store failed to recover.");
            }

            while (this.chain.GetBlock(tipHeight) == null)
            {
                if (latestHeader.HashPrevBlock == this.chain.Genesis.HashBlock)
                {
                    latestBlockHash = this.chain.Genesis.HashBlock;
                    break;
                }

                latestHeader = await this.provenBlockHeaderRepository.GetAsync(tipHeight--).ConfigureAwait(false);

                latestBlockHash = latestHeader.GetHash();
            }

            ChainedHeader newTip = this.chain.GetBlock(tipHeight);

            this.logger.LogWarning("Proven block header store tip recovered to block '{0}'.", newTip);

            return newTip;
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            if (this.storeTip != null)
            {
                benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

                BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

                if (this.latestPerformanceSnapShot == null)
                    benchLog.AppendLine(snapShot.ToString());
                else
                    benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

                this.latestPerformanceSnapShot = snapShot;
            }

            this.PendingBatch.Sum(p => p.Value.HeaderSize);
        }

        private void AddComponentStats(StringBuilder log)
        {
            long totalBytes = this.PendingBatch.Sum(p => p.Value.HeaderSize);

            decimal totalInMB = Convert.ToDecimal(totalBytes / Math.Pow(2, 20));

            if ((this.storeTip != null) && (totalBytes > 0))
            {
                log.AppendLine();
                log.AppendLine("======ProvenBlockHeaderStore======");
                log.AppendLine($"Batch Size: {Math.Round(totalInMB, 2)} Mb ({this.PendingBatch.Count} headers)");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}