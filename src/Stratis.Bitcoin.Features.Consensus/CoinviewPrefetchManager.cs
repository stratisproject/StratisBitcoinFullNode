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
    /// <summary>Fetches coins for blocks that are likely to be validated in the near future to speedup full validation.</summary>
    public class CoinviewPrefetchManager : IDisposable
    {
        /// <summary>
        /// How many blocks ahead prefetching will look.
        /// When header at height X is dequeued block with header at height <c>X + Lookahead</c> will be prefetched in case block data is downloaded.
        /// </summary>
        private const int Lookahead = 5;

        /// <summary>Queue of headers that were added when block associated with such header was fully validated.</summary>
        private readonly AsyncQueue<ChainedHeader> headersQueue;

        private readonly ICoinView coinview;

        private readonly CoinviewHelprer coinviewHelper;

        private readonly ILogger logger;

        public CoinviewPrefetchManager(ICoinView coinview, ILoggerFactory loggerFactory)
        {
            this.coinview = coinview;

            this.headersQueue = new AsyncQueue<ChainedHeader>(this.OnHeaderEnqueuedAsync);
            this.coinviewHelper = new CoinviewHelprer();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Prefetches UTXOs for a block and some blocks with higher height.
        /// Prefetching is done on the background.
        /// </summary>
        /// <param name="header">Header of a block that is about to be partially validated and requires prefetching.</param>
        private async Task OnHeaderEnqueuedAsync(ChainedHeader header, CancellationToken cancellation)
        {
            ChainedHeader currentHeader = header;

            // Go Lookahead blocks ahead of current header and get block for prefetching.
            // There might be several blocks at height of header.Height + Lookahead but
            // only first one will be prefetched since prefetching is in place mostly to
            // speed up IBD in which we can have only one chain on Stratis since we have
            // checkpoints and several chains on BTC but it's a rare case because creating
            // alternative chains requires PoW which is expensive.
            for (int i = 0; i < Lookahead; i++)
            {
                if (currentHeader.Next.Count == 0)
                {
                    this.logger.LogTrace("(-)[NO_HEADERS]");
                    return;
                }

                currentHeader = currentHeader.Next[0];
            }

            //TODO skip prefetching if CT is ahead of block that we want to prefetch

            Block block = currentHeader.Block;

            if (block == null)
            {
                this.logger.LogTrace("(-)[NO_BLOCK_DATA]");
                return;
            }

            bool enforceBIP30 = DeploymentFlags.EnforceBIP30ForBlock(currentHeader);
            uint256[] idsToFetch = this.coinviewHelper.GetIdsToFetch(block, enforceBIP30);

            if (idsToFetch.Length != 0 && !cancellation.IsCancellationRequested)
            {
                await this.coinview.FetchCoinsAsync(idsToFetch, cancellation).ConfigureAwait(false);

                this.logger.LogDebug("{0} ids were prefetched.", idsToFetch.Length);
            }
        }

        /// <summary>
        /// Prefetches UTXOs for a block and some blocks with higher height.
        /// Prefetching is done on the background.
        /// </summary>
        /// <param name="header">Header of a block that was fully validated and requires prefetching.</param>
        public void Prefetch(ChainedHeader header)
        {
            this.headersQueue.Enqueue(header);
        }

        public void Dispose()
        {
            this.headersQueue.Dispose();
        }
    }
}
