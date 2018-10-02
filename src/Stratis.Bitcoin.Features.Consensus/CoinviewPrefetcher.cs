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
    /// <summary>Fetches coins for blocks that are likely to be validated in the near future to speed-up full validation.</summary>
    public class CoinviewPrefetcher : IDisposable
    {
        /// <summary>
        /// How many blocks ahead pre-fetching will look.
        /// When header at height X is dequeued block with header at height <c>X + Lookahead</c> will be pre-fetching in case block data is downloaded.
        /// </summary>
        /// <remarks>
        /// TODO maybe make it dynamic so the value is increased when we are too close to the tip after pre-fetching was completed
        /// and decreased if we are too far from the tip.
        /// </remarks>
        private const int Lookahead = 20;

        /// <summary>Queue of headers that were added when block associated with such header was fully validated.</summary>
        private readonly AsyncQueue<ChainedHeader> headersQueue;

        private readonly ICoinView coinview;

        private readonly CoinviewHelper coinviewHelper;

        private readonly ConcurrentChain chain;

        private readonly ILogger logger;

        public CoinviewPrefetcher(ICoinView coinview, ConcurrentChain chain, ILoggerFactory loggerFactory)
        {
            this.coinview = coinview;
            this.chain = chain;

            this.headersQueue = new AsyncQueue<ChainedHeader>(this.OnHeaderEnqueuedAsync);
            this.coinviewHelper = new CoinviewHelper();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Pre-fetches UTXOs for a block and some blocks with higher height.
        /// pre-fetching is done on the background.
        /// </summary>
        /// <param name="header">Header of a block that is about to be partially validated and requires pre-fetching.</param>
        private async Task OnHeaderEnqueuedAsync(ChainedHeader header, CancellationToken cancellation)
        {
            ChainedHeader currentHeader = header;

            // Go Lookahead blocks ahead of current header and get block for pre-fetching.
            // There might be several blocks at height of header.Height + Lookahead but
            // only first one will be pre-fetched since pre-fetching is in place mostly to
            // speed up IBD.
            for (int i = 0; i < Lookahead; i++)
            {
                if (currentHeader.Next.Count == 0)
                {
                    this.logger.LogTrace("(-)[NO_HEADERS]");
                    return;
                }

                currentHeader = currentHeader.Next.FirstOrDefault();

                if (currentHeader == null)
                {
                    this.logger.LogTrace("(-)[NO_NEXT_HEADER]");
                    return;
                }
            }

            Block block = currentHeader.Block;

            if (block == null)
            {
                this.logger.LogTrace("(-)[NO_BLOCK_DATA]");
                return;
            }

            bool farFromTip = currentHeader.Height > this.chain.Tip.Height + (Lookahead / 2);

            if (!farFromTip)
            {
                this.logger.LogDebug("Block selected for pre-fetching is too close to the tip! Skipping pre-fetching.");
                this.logger.LogTrace("(-)[TOO_CLOSE_TO_PREFETCH]");
                return;
            }

            bool enforceBIP30 = DeploymentFlags.EnforceBIP30ForBlock(currentHeader);
            uint256[] idsToFetch = this.coinviewHelper.GetIdsToFetch(block, enforceBIP30);

            if (idsToFetch.Length != 0)
            {
                await this.coinview.FetchCoinsAsync(idsToFetch, cancellation).ConfigureAwait(false);

                this.logger.LogTrace("{0} ids were pre-fetched.", idsToFetch.Length);
            }
        }

        /// <summary>
        /// Pre-fetches UTXOs for a block and some blocks with higher height.
        /// Pre-fetching is done on the background.
        /// </summary>
        /// <param name="header">Header of a block that was fully validated and requires pre-fetching.</param>
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
