using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>Fetches coins for blocks that are likely to be validated in the very near future to speedup full validation.</summary>
    public class CoinviewPrefetchManager : IDisposable
    {
        /// <summary>How many blocks ahead worth of trids we should ptry to prefetch.</summary>
        private const int TargetMaxIdsToPrefetch = 5_000;

        /// <summary>How many most recent block ids that were prefetched should be in the memory.</summary>
        private const int MaxPrefetchedBlocksHistory = 1_000;

        /// <summary>Queue of headers that were added when block associated with such header was about to be partially validated.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        private readonly Queue<ChainedHeader> headersQueue;

        /// <summary>Protects access to <see cref="headersQueue"/>.</summary>
        private readonly object locker;

        private readonly ICoinView coinview;

        /// <summary>Task that prefetches UTXOs.</summary>
        private readonly Task prefetchingTask;

        private readonly CancellationTokenSource cancellation;

        /// <summary>Event that is triggered when new item is added to <see cref="headersQueue"/>.</summary>
        private readonly AsyncManualResetEvent itemEnqueuedEvent;

        private readonly CoinviewHelprer coinviewHelper;

        /// <summary>Ids of blocks that were already prefetched.</summary>
        private MemoryCache<uint256, int> prefetchedBlocks;

        private readonly DeploymentFlags flags;

        private readonly ILogger logger;

        public CoinviewPrefetchManager(ICoinView coinview, ILoggerFactory loggerFactory)
        {
            this.coinview = coinview;

            this.headersQueue = new Queue<ChainedHeader>();
            this.locker = new object();
            this.coinviewHelper = new CoinviewHelprer();
            this.cancellation = new CancellationTokenSource();
            this.prefetchedBlocks = new MemoryCache<uint256, int>(MaxPrefetchedBlocksHistory);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.flags = new DeploymentFlags();

            this.itemEnqueuedEvent = new AsyncManualResetEvent(false);
            this.prefetchingTask = this.ProcessQueueContinouslyAsync();
        }

        /// <summary>
        /// Prefetches UTXOs for a block and some blocks with higher height.
        /// Prefetching is done on the background.
        /// </summary>
        /// <param name="header">Header of a block that is about to be partially validated and requires prefetching.</param>
        public void Prefetch(ChainedHeader header)
        {
            lock (this.locker)
            {
                this.headersQueue.Enqueue(header);
                this.itemEnqueuedEvent.Set();
            }
        }

        private async Task ProcessQueueContinouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await this.itemEnqueuedEvent.WaitAsync(this.cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var txesToPrefetch = new List<uint256>();

                lock (this.locker)
                {
                    while (this.headersQueue.Count > 0)
                    {
                        ChainedHeader header = this.headersQueue.Dequeue();
                        Block block = header.Block;

                        // Skip this header if block is null or it was prefetched already.
                        if (this.prefetchedBlocks.TryGetValue(header.HashBlock, out int unused) || (block == null))
                        {
                            this.logger.LogTrace("Skipping block '{0}' because it was already prefetched or block data is null.", header);
                            continue;
                        }

                        uint256[] ids = this.GetFetchIds(header, block);
                        txesToPrefetch.AddRange(ids);

                        this.logger.LogTrace("{0} ids to prefetch were added for the block '{1}'.", ids.Length, header);

                        if (this.headersQueue.Count == 0)
                        {
                            int limit = TargetMaxIdsToPrefetch - txesToPrefetch.Count;

                            List<uint256> lookaheadIds = this.GetLookAheadFetchIds(limit, header);
                            txesToPrefetch.AddRange(lookaheadIds);

                            this.logger.LogDebug("{0} lookahead ids to prefetch were added.", lookaheadIds.Count);
                        }
                    }
                }

                if (txesToPrefetch.Count != 0 && !this.cancellation.IsCancellationRequested)
                {
                    await this.coinview.FetchCoinsAsync(txesToPrefetch.ToArray()).ConfigureAwait(false);

                    this.logger.LogDebug("{0} ids were prefetched.", txesToPrefetch.Count);
                }
            }
        }

        /// <summary>Gets tx ids to prefetch for a specified block.</summary>
        private uint256[] GetFetchIds(ChainedHeader header, Block block)
        {
            this.flags.ConfigureEnforceBIP30Flag(header);

            uint256[] idsToFetch = this.coinviewHelper.GetIdsToFetch(block, this.flags.EnforceBIP30);
            this.prefetchedBlocks.AddOrUpdate(header.HashBlock, header.Height);

            return idsToFetch;
        }

        /// <summary>Gets tx ids to prefetch for blocks that will most likely be validated in the future.</summary>
        private List<uint256> GetLookAheadFetchIds(int targetIdsLimit, ChainedHeader lastFetchedHeader)
        {
            var txIdsToFetch = new List<uint256>();
            var headersToFetch = new List<ChainedHeader>(lastFetchedHeader.Next);

            int limitLeft = targetIdsLimit;

            while ((limitLeft > 0) && (headersToFetch.Count != 0))
            {
                var nextHeadersToFetch = new List<ChainedHeader>();

                // Width first approach.
                foreach (ChainedHeader header in headersToFetch)
                {
                    Block block = header.Block;

                    if (this.prefetchedBlocks.TryGetValue(header.HashBlock, out int unused) || (block == null))
                        continue;

                    uint256[] fetchIdsForHeader = this.GetFetchIds(header, block);
                    txIdsToFetch.AddRange(fetchIdsForHeader);

                    limitLeft -= fetchIdsForHeader.Length;

                    nextHeadersToFetch.AddRange(header.Next);
                }

                if (limitLeft <= 0 && nextHeadersToFetch.Count != 0)
                    this.logger.LogDebug("Lookahead limit reached. Latest block processed is '{0}'.", nextHeadersToFetch.First());

                headersToFetch = nextHeadersToFetch;
            }

            return txIdsToFetch;
        }

        public void Dispose()
        {
            this.cancellation.Cancel();
            this.prefetchingTask.GetAwaiter().GetResult();
        }
    }
}
