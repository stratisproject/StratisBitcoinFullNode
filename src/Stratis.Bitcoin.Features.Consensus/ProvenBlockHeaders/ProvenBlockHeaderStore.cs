using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
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
    /// Items in the pending batch are also saved to the least recently used <see cref="MemorySizeCache{int, ProvenBlockHeader}"/>. Where the memory size is limited by <see cref="MemoryCacheSizeLimitInBytes"/>.
    /// </para>
    /// <para>
    /// When new <see cref="ProvenBlockHeader"/> items are saved to the database - in case <see cref="IProvenBlockHeaderRepository"/> contains headers that
    /// are no longer a part of the best chain - they are overwritten or ignored.
    /// </para>
    /// <para>
    /// When <see cref="ProvenBlockHeaderStore"/> is being initialized it will overwrite blocks that are not on the best chain.
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
        /// The highest stored <see cref= "ChainedHeader"/> tip in the store.
        /// </summary>
        private ChainedHeader storeTip;

        /// <summary>
        /// Pending - not yet saved to disk - <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// <para>
        /// All access to these items have to be protected by <see cref="lockObject" />
        /// </para>
        /// </summary>
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
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"/> DBreeze repository.</param>
        /// <param name="nodeLifetime">Allows consumers to perform clean-up during a graceful shutdown.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        /// <param name="asyncLoopFactory">Factory for creating and also possibly starting application defined tasks inside async loop.</param>
        public ProvenBlockHeaderStore(
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeLifetime nodeLifetime,
            INodeStats nodeStats,
            IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(nodeStats, nameof(nodeStats));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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

        /// <inheritdoc />
        public async Task InitializeAsync(ChainedHeader chainedHeader)
        {
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            if (chainedHeader.Height > 0)
            {
                ProvenBlockHeader repoHeader =
                    await this.provenBlockHeaderRepository.GetAsync(chainedHeader.Height);

                if (repoHeader == null)
                    throw new ProvenBlockHeaderException("Unable to find proven block header in the repository.");

                if (repoHeader.GetHash() != chainedHeader.HashBlock)
                    throw new ProvenBlockHeaderException("Chain header tip hash does not match the latest proven block header hash saved to disk.");
            }

            this.storeTip = chainedHeader;

            this.TipHashHeight = new HashHeightPair(chainedHeader.HashBlock, chainedHeader.Height);

            this.logger.LogDebug("Proven block header store tip at block '{0}'.", this.TipHashHeight);

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
                if (!this.Cache.TryGetValue(blockHeight, out header))
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

            var provenHeadersOutput = new SortedDictionary<int, ProvenBlockHeader>();

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                int index = fromBlockHeight;

                ProvenBlockHeader header = null;

                var blockHeightsNotInCache = new List<int>();

                do
                {
                    if (this.Cache.TryGetValue(index, out header))
                    {
                        provenHeadersOutput.Add(index, header);
                    }
                    else
                    {
                        blockHeightsNotInCache.Add(index);
                    }

                    index++;

                } while (index <= toBlockHeight);

                // Try and get items from the repository if not found in the store cache.
                if (blockHeightsNotInCache.Count > 0)
                {
                    // If headersInCache is empty then we can assume blockHeightsNotInCache is the full range.
                    if (provenHeadersOutput.Keys.Count == 0)
                    {
                        List<ProvenBlockHeader> rangeHeaders = await this.provenBlockHeaderRepository.GetAsync(fromBlockHeight, toBlockHeight);

                        index = fromBlockHeight;

                        foreach (ProvenBlockHeader rangeHeader in rangeHeaders)
                        {
                            if (rangeHeader != null)
                            {
                                provenHeadersOutput.Add(index, rangeHeader);
                                this.Cache.AddOrUpdate(index, rangeHeader, rangeHeader.HeaderSize);
                            }

                            index++;
                        }
                    }
                    else
                    {
                        // If not a full sequence then check individually.
                        foreach (int headerNotInCache in blockHeightsNotInCache)
                        {
                            ProvenBlockHeader repositoryHeader = await this.provenBlockHeaderRepository.GetAsync(headerNotInCache).ConfigureAwait(false);

                            if (repositoryHeader != null)
                            {
                                provenHeadersOutput.Add(headerNotInCache, repositoryHeader);

                                this.Cache.AddOrUpdate(headerNotInCache, repositoryHeader, repositoryHeader.HeaderSize);
                            }
                        }
                    }
                }
            }

            this.CheckItemsAreInConsecutiveSequence(provenHeadersOutput.Keys.ToList());

            return provenHeadersOutput.Values.ToList();
        }

        /// <inheritdoc />
        public void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip)
        {
            lock (this.lockObject)
            {
                this.PendingBatch.Add(newTip.Height, provenBlockHeader);

                this.pendingTipHashHeight = newTip;
            }

            this.Cache.AddOrUpdate(newTip.Height, provenBlockHeader, provenBlockHeader.HeaderSize);
        }

        /// <summary>
        /// Saves pending <see cref="ProvenBlockHeader"/> items to the <see cref="IProvenBlockHeaderRepository"/>, then removes the items from the pending batch.
        /// </summary>
        private async Task SaveAsync()
        {
            if (this.pendingTipHashHeight == null)
                return;

            Dictionary<int, ProvenBlockHeader> pendingBatch;

            HashHeightPair hashHeight = null;

            lock (this.lockObject)
            {
                pendingBatch = new Dictionary<int, ProvenBlockHeader>(this.PendingBatch);

                this.PendingBatch.Clear();

                hashHeight = this.pendingTipHashHeight;

                this.pendingTipHashHeight = null;
            }

            this.CheckItemsAreInConsecutiveSequence(pendingBatch.Keys.ToList());

            // Save the items to disk.
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                // Save the items to disk.
                await this.provenBlockHeaderRepository.PutAsync(pendingBatch.Values.ToList(), hashHeight).ConfigureAwait(false);

                this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;
            }
        }

        /// <summary>
        /// Checks whether block height keys are in consecutive sequence.
        /// </summary>
        /// <param name="keys">List of block height keys to check.</param>
        private void CheckItemsAreInConsecutiveSequence(List<int> keys)
        {
            if (!keys.SequenceEqual(Enumerable.Range(keys.First(), keys.Count())))
            {
                this.logger.LogTrace("(-)[PROVEN_BLOCK_HEADERS_NOT_IN_CONSECUTIVE_SEQEUNCE]");

                throw new ProvenBlockHeaderException("Proven block headers are not in the correct consecutive sequence.");
            }
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

            if (totalBytes == 0) return;

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