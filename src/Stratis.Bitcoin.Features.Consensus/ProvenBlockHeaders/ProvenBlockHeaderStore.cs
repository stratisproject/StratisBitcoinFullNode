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
using TracerAttributes;

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
        /// Pending - not yet saved to disk - <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// <para>
        /// All access to these items have to be protected by <see cref="lockObject" />
        /// </para>
        /// </summary>
        private HashHeightPair pendingTipHashHeight;

        /// <summary>
        /// A lock object that protects access to the <see cref="PendingBatch"/> and <see cref="pendingTipHashHeight"/>.
        /// </summary>
        private readonly object lockObject;

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
        public readonly SortedDictionary<int, ProvenBlockHeader> PendingBatch;

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
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderStore(
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeStats nodeStats)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));
            Guard.NotNull(nodeStats, nameof(nodeStats));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;

            this.lockObject = new object();
            this.PendingBatch = new SortedDictionary<int, ProvenBlockHeader>();
            this.Cache = new MemorySizeCache<int, ProvenBlockHeader>(this.MemoryCacheSizeLimitInBytes);

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <inheritdoc />
        public async Task<ChainedHeader> InitializeAsync(ChainedHeader highestHeader)
        {
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            ChainedHeader tip = highestHeader;
            HashHeightPair repoTip = this.provenBlockHeaderRepository.TipHashHeight;

            if (repoTip.Hash != tip.HashBlock)
            {
                // Repository is behind chain of headers.
                tip = tip.FindAncestorOrSelf(repoTip.Hash, repoTip.Height);
            }

            this.TipHashHeight = new HashHeightPair(tip.HashBlock, tip.Height);
            this.logger.LogDebug("Proven block header store initialized at '{0}'.", this.TipHashHeight);

            return tip;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                if (this.Cache.TryGetValue(blockHeight, out ProvenBlockHeader header))
                    return header;

                // Check the repository.
                header = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                if (header != null)
                    this.Cache.AddOrUpdate(blockHeight, header, header.HeaderSize);

                return header;
            }
        }

        /// <inheritdoc />
        public async Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            if (fromBlockHeight >= toBlockHeight)
                throw new ArgumentException($"{nameof(fromBlockHeight)} can't be equal or greater than {nameof(toBlockHeight)}");

            var provenHeadersOutput = new SortedDictionary<int, ProvenBlockHeader>();

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                int index = fromBlockHeight;

                var blockHeightsNotInCache = new List<int>();

                do
                {
                    if (this.Cache.TryGetValue(index, out ProvenBlockHeader header))
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
                // If an item is already there this means a reorg happened.
                // We always assume the latest header belongs to the longest chain so just overwrite the previous values.
                this.PendingBatch.AddOrReplace(newTip.Height, provenBlockHeader);

                this.pendingTipHashHeight = newTip;
            }

            this.Cache.AddOrUpdate(newTip.Height, provenBlockHeader, provenBlockHeader.HeaderSize);
        }

        /// <inheritdoc />
        public async Task SaveAsync()
        {
            if (this.pendingTipHashHeight == null)
            {
                this.logger.LogTrace("(-)[PENDING_HEIGHT_NULL]");
                return;
            }

            SortedDictionary<int, ProvenBlockHeader> pendingBatch;
            HashHeightPair hashHeight;

            lock (this.lockObject)
            {
                pendingBatch = new SortedDictionary<int, ProvenBlockHeader>(this.PendingBatch);

                this.PendingBatch.Clear();

                hashHeight = this.pendingTipHashHeight;

                this.pendingTipHashHeight = null;
            }

            if (pendingBatch.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_PROVEN_HEADER_ITEMS]");
                return;
            }

            this.CheckItemsAreInConsecutiveSequence(pendingBatch.Keys.ToList());

            // Save the items to disk.
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                // Save the items to disk.
                await this.provenBlockHeaderRepository.PutAsync(pendingBatch, hashHeight).ConfigureAwait(false);

                this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;
            }
        }

        /// <summary>
        /// Checks whether block height keys are in consecutive sequence.
        /// </summary>
        /// <param name="keys">List of block height keys to check.</param>
        private void CheckItemsAreInConsecutiveSequence(List<int> keys)
        {
            if (!keys.SequenceEqual(Enumerable.Range(keys.First(), keys.Count)))
            {
                this.logger.LogTrace("(-)[PROVEN_BLOCK_HEADERS_NOT_IN_CONSECUTIVE_SEQEUNCE]");
                throw new ProvenBlockHeaderException("Proven block headers are not in the correct consecutive sequence.");
            }
        }

        [NoTrace]
        private void AddBenchStats(StringBuilder benchLog)
        {
            if (this.TipHashHeight != null)
            {
                benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

                BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

                if (this.latestPerformanceSnapShot == null)
                    benchLog.AppendLine(snapShot.ToString());
                else
                    benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

                this.latestPerformanceSnapShot = snapShot;
            }
        }

        [NoTrace]
        private void AddComponentStats(StringBuilder log)
        {
            long totalBytes = 0;
            int count = 0;

            lock (this.lockObject)
            {
                totalBytes = this.PendingBatch.Sum(p => p.Value.HeaderSize);
                count = this.PendingBatch.Count;
            }

            if (totalBytes == 0) return;

            decimal totalInMB = Convert.ToDecimal(totalBytes / Math.Pow(2, 20));

            if ((this.TipHashHeight != null) && (totalBytes > 0))
            {
                log.AppendLine();
                log.AppendLine("======ProvenBlockHeaderStore======");
                log.AppendLine($"Batch Size: {Math.Round(totalInMB, 2)} Mb ({count} headers)");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}