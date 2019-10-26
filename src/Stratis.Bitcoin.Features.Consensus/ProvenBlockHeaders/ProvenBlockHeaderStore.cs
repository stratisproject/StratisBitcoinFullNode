﻿using System;
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
    /// Items in the pending batch are also saved to the least recently used <see cref="MemorySizeCache{int, ProvenBlockHeader}"/>.
    /// Where the memory size is limited by <see cref="MemoryCacheSizeLimitInBytes"/>.
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
        private readonly ILogger logger;

        /// <summary>Database repository storing <see cref="ProvenBlockHeader"/> items.</summary>
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Performance counter to measure performance of the save and get operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;

        /// <summary>Latest snapshot performance counter to measure performance of the save and get operations.</summary>
        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>
        /// Current <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// </summary>
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Pending - not yet saved to disk - <see cref="IProvenBlockHeaderStore"/> tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.
        /// <para>All access to these items have to be protected by <see cref="lockObject" /></para>
        /// </summary>
        private HashHeightPair pendingTipHashHeight;

        /// <summary>A lock object that protects access to the <see cref="pendingBatch"/> and <see cref="pendingTipHashHeight"/>.</summary>
        private readonly object lockObject;

        /// <summary>Cache limit.</summary>
        private readonly long MemoryCacheSizeLimitInBytes = 50 * 1024 * 1024;

        /// <summary>
        /// Cache of pending <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Pending <see cref= "ProvenBlockHeader"/> items will be saved to disk every minute.
        /// <para>
        /// All access to these items have to be protected by <see cref="lockObject"/>.
        /// </para>
        /// </remarks>
        private readonly SortedDictionary<int, ProvenBlockHeader> pendingBatch;

        /// <summary>
        /// This is a work around to fail the node if the save operation fails.
        /// TODO: Use a global node exception that will stop consensus in critical errors.
        /// </summary>
        private Exception saveAsyncLoopException;

        /// <summary>
        /// Store Cache of <see cref= "ProvenBlockHeader"/> items.
        /// </summary>
        /// <remarks>
        /// Items are added to this cache when the caller asks for a <see cref= "ProvenBlockHeader"/>.
        /// </remarks>
        public MemorySizeCache<int, ProvenBlockHeader> Cache { get; }

        public ProvenBlockHeaderStore(
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeStats nodeStats,
            IInitialBlockDownloadState initialBlockDownloadState)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));
            Guard.NotNull(nodeStats, nameof(nodeStats));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;
            this.initialBlockDownloadState = initialBlockDownloadState;

            this.lockObject = new object();
            this.pendingBatch = new SortedDictionary<int, ProvenBlockHeader>();
            this.Cache = new MemorySizeCache<int, ProvenBlockHeader>(this.MemoryCacheSizeLimitInBytes);

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, this.GetType().Name);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name);
        }

        /// <inheritdoc />
        public async Task<ChainedHeader> InitializeAsync(ChainedHeader highestHeader)
        {
            Guard.NotNull(highestHeader, nameof(highestHeader));
            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            ChainedHeader tip = highestHeader;
            HashHeightPair repoTip = this.provenBlockHeaderRepository.TipHashHeight;

            if (repoTip.Hash != tip.HashBlock)
            {
                // Repository is behind chain of headers.
                tip = tip.FindAncestorOrSelf(repoTip.Hash, repoTip.Height);

                if (tip == null)
                {
                    // Start at one less of the current repo height as we have already checked
                    // the repo tip.
                    for (int height = repoTip.Height - 1; height > 0; height--)
                    {
                        ProvenBlockHeader provenBlockHeader = await this.provenBlockHeaderRepository.GetAsync(height).ConfigureAwait(false);

                        // Block header at current height not found, go to previous height.
                        if (provenBlockHeader == null) continue;

                        tip = highestHeader.FindAncestorOrSelf(provenBlockHeader.GetHash());
                        if (tip != null)
                        {
                            this.TipHashHeight = new HashHeightPair(provenBlockHeader.GetHash(), height);
                            break;
                        }
                    }

                    if (tip == null)
                    {
                        this.logger.LogDebug("[TIP_NOT_FOUND]:{0}", highestHeader);
                        throw new ProvenBlockHeaderException($"{highestHeader} was not found in the store.");
                    }
                }
                else
                    this.TipHashHeight = new HashHeightPair(tip.HashBlock, tip.Height);
            }
            else
                this.TipHashHeight = new HashHeightPair(tip.HashBlock, tip.Height);

            this.logger.LogDebug("Proven block header store initialized at '{0}'.", this.TipHashHeight);

            return tip;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                lock (this.lockObject)
                {
                    if (this.pendingBatch.TryGetValue(blockHeight, out ProvenBlockHeader headerFromBatch))
                    {
                        this.logger.LogTrace("(-)[FROM_BATCH]");
                        return headerFromBatch;
                    }
                }

                if (this.Cache.TryGetValue(blockHeight, out ProvenBlockHeader header))
                {
                    this.logger.LogTrace("(-)[FROM_CACHE]");
                    return header;
                }

                // Check the repository.
                header = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);

                if (header != null)
                {
                    this.Cache.AddOrUpdate(blockHeight, header, header.HeaderSize);
                    this.logger.LogTrace("(-)[FROM_REPO]");
                    return header;
                }

                return header;
            }
        }

        /// <inheritdoc />
        public void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip)
        {
            this.logger.LogDebug("({0}:'{1}',{2}:'{3}')", nameof(provenBlockHeader), provenBlockHeader, nameof(newTip), newTip);

            Guard.Assert(provenBlockHeader.GetHash() == newTip.Hash);

            // Stop the consensus loop.
            if (this.saveAsyncLoopException != null)
                throw this.saveAsyncLoopException;

            lock (this.lockObject)
            {
                if ((this.pendingTipHashHeight != null) && (provenBlockHeader.HashPrevBlock != this.pendingTipHashHeight.Hash))
                {
                    // The latest proven header to be added is the most recent from consensus
                    // and is always assumed to be the consensus tip.
                    // If a non-consecutive item is added then there may have been a reorg in the chain
                    // this can happen after the node rewind its consensus.

                    // Walk back the batch and remove all the blocks that are on the fork.
                    KeyValuePair<int, ProvenBlockHeader> lastItem = this.pendingBatch.Last();
                    while (provenBlockHeader.HashPrevBlock != lastItem.Value.GetHash())
                    {
                        this.pendingBatch.Remove(lastItem.Key);
                        this.Cache.Remove(lastItem.Key);

                        if (this.pendingBatch.Count == 0)
                            break;

                        lastItem = this.pendingBatch.Last();
                    }
                }

                // If an item is already there this means a reorg happened.
                // We always assume the latest header belongs to the longest chain so just overwrite the previous values.
                this.pendingBatch.AddOrReplace(newTip.Height, provenBlockHeader);

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

            try
            {
                SortedDictionary<int, ProvenBlockHeader> pendingBatchInsert;
                HashHeightPair hashHeight;

                lock (this.lockObject)
                {
                    pendingBatchInsert = new SortedDictionary<int, ProvenBlockHeader>(this.pendingBatch);

                    this.pendingBatch.Clear();

                    hashHeight = this.pendingTipHashHeight;

                    this.pendingTipHashHeight = null;
                }

                if (pendingBatchInsert.Count == 0)
                {
                    this.logger.LogTrace("(-)[NO_PROVEN_HEADER_ITEMS]");
                    return;
                }

                this.CheckItemsAreInConsecutiveSequence(pendingBatchInsert.Keys.ToList());

                // Save the items to disk.
                using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                {
                    // Save the items to disk.
                    await this.provenBlockHeaderRepository.PutAsync(pendingBatchInsert, hashHeight).ConfigureAwait(false);

                    this.TipHashHeight = this.provenBlockHeaderRepository.TipHashHeight;
                }

                if (this.initialBlockDownloadState.IsInitialBlockDownload())
                {
                    // During IBD the PH cache is not used much,
                    // to avoid occupying unused space in memory we flush the cache.
                    foreach (KeyValuePair<int, ProvenBlockHeader> provenBlockHeader in pendingBatchInsert)
                    {
                        this.Cache.Remove(provenBlockHeader.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                this.saveAsyncLoopException = ex;
                this.logger.LogError("Error saving the batch {0}", ex);
                throw;
            }
        }

        /// <summary>Checks whether block height keys are in consecutive sequence.</summary>
        /// <param name="keys">List of block height keys to check.</param>
        private void CheckItemsAreInConsecutiveSequence(List<int> keys)
        {
            if (!keys.SequenceEqual(Enumerable.Range(keys.First(), keys.Count)))
            {
                this.logger.LogError("(-)[PROVEN_BLOCK_HEADERS_NOT_IN_CONSECUTIVE_SEQUENCE]: {0}", string.Join(",", keys));
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
            if (this.TipHashHeight == null)
                return;

            long totalBytes = 0;
            int count = 0;

            lock (this.lockObject)
            {
                totalBytes = this.pendingBatch.Sum(p => p.Value.HeaderSize);
                count = this.pendingBatch.Count;
            }

            decimal totalCacheInMb = Convert.ToDecimal(this.Cache.TotalSize / Math.Pow(2, 20));
            decimal totalMaxCacheInMb = Convert.ToDecimal(this.Cache.MaxSize / Math.Pow(2, 20));
            decimal totalBatchInMb = Convert.ToDecimal(totalBytes / Math.Pow(2, 20));

            log.AppendLine();
            log.AppendLine("======ProvenBlockHeaderStore======");
            log.AppendLine($"Batch Size: {Math.Round(totalBatchInMb, 2)} Mb ({count} headers)");
            log.AppendLine($"Cache Size: {Math.Round(totalCacheInMb, 2)}/{Math.Round(totalMaxCacheInMb, 2)} MB");

        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.provenBlockHeaderRepository.Dispose();
        }
    }
}