using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public interface IMaturedBlocksProvider
    {
        /// <summary>
        /// Retrieves deposits for the indicated blocks from the block repository and throws an error if the blocks are not mature enough.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="maxBlocks">The number of blocks to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the blocks are not mature or not found.</exception>
        Task<List<MaturedBlockDepositsModel>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks);
    }

    public class MaturedBlocksProvider : IMaturedBlocksProvider
    {
        private readonly IDepositExtractor depositExtractor;

        private readonly IConsensusManager consensusManager;

        private readonly ILogger logger;

        private Dictionary<int, MaturedBlockDepositsModel> depositCache;

        public MaturedBlocksProvider(ILoggerFactory loggerFactory, IDepositExtractor depositExtractor, IConsensusManager consensusManager)
        {
            this.depositExtractor = depositExtractor;
            this.consensusManager = consensusManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.depositCache = new Dictionary<int, MaturedBlockDepositsModel>();
        }

        /// <inheritdoc />
        public async Task<List<MaturedBlockDepositsModel>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks)
        {
            ChainedHeader consensusTip = this.consensusManager.Tip;

            int matureTipHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (blockHeight > matureTipHeight)
            {
                throw new InvalidOperationException($"Block height {blockHeight} submitted is not mature enough. Blocks less than a height of {matureTipHeight} can be processed.");
            }

            // Cache clean-up.
            lock (this.depositCache)
            {
                // The requested height gives away the fact that the peer is probably no longer interested in cached entries below that height.
                // Keep an additional 1,000 blocks anyway in case there are some parallel request that are still executing for lower heights.
                foreach (int i in this.depositCache.Where(d => d.Key < (blockHeight - 1000)).Select(d => d.Key).ToArray())
                    this.depositCache.Remove(i);
            }

            var maturedBlocks = new List<MaturedBlockDepositsModel>();

            // Don't spend to much time that the requester may give up.
            DateTime deadLine = DateTime.Now.AddSeconds(30);

            for (int i = blockHeight; (i <= matureTipHeight) && (i < blockHeight + maxBlocks); i++)
            {
                MaturedBlockDepositsModel maturedBlockDeposits = null;

                // First try the cache.
                lock (this.depositCache)
                {
                    this.depositCache.TryGetValue(i, out maturedBlockDeposits);
                }

                // If not in cache..
                if (maturedBlockDeposits == null)
                {
                    ChainedHeader currentHeader = consensusTip.GetAncestor(i);
                    ChainedHeaderBlock block = await this.consensusManager.GetBlockDataAsync(currentHeader.HashBlock).ConfigureAwait(false);
                    maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(block);

                    if (maturedBlockDeposits == null)
                        throw new InvalidOperationException($"Unable to get deposits for block at height {currentHeader.Height}");

                    // Save this so that we don't need to scan the block again.
                    lock (this.depositCache)
                    {
                        this.depositCache[i] = maturedBlockDeposits;
                    }
                }

                maturedBlocks.Add(maturedBlockDeposits);

                if (DateTime.Now >= deadLine)
                    break;
            }

            return maturedBlocks;
        }
    }
}