﻿using System;
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
        /// Current block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Pending - not yet saved to disk - block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
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
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 100);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;

            this.logger.LogDebug("Initialized ProvenBlockHeader block tip at '{0}'.", this.TipHashHeight);

            this.asyncLoop = this.asyncLoopFactory.Run("ProvenBlockHeaders job", async token =>
            {
                // Save pending items.
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

            this.CheckItemsAreInSequence(provenHeadersOutput.Keys.ToList());

            return provenHeadersOutput.Values.ToList();
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
        /// Saves pending <see cref="ProvenBlockHeader"/> items to the <see cref="IProvenBlockHeaderRepository"/>.
        /// <para>
        /// It will also remove the items from the <see cref="PendingBatch"/>.
        /// </para>
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

            this.CheckItemsAreInSequence(pendingBatch.Keys.ToList());

            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                // Save the items to disk.
                await this.provenBlockHeaderRepository.PutAsync(pendingBatch.Values.ToList(), hashHeight);

                this.TipHashHeight = hashHeight;
            }
        }

        /// <summary>
        /// Checks whether block height keys are in sequence.
        /// </summary>
        /// <param name="keys">List of block height keys to check.</param>
        private void CheckItemsAreInSequence(List<int> keys)
        {
            if (!keys.SequenceEqual(Enumerable.Range(keys.First(), keys.Count())))
            {
                this.logger.LogTrace("(-)[PROVEN_BLOCK_HEADERS_NOT_IN_SEQEUNCE]");

                throw new InvalidOperationException("Proven block headers are not in the correct sequence.");
            }
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
