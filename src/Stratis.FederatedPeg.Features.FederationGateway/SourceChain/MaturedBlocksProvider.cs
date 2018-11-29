using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class MaturedBlocksProvider : IMaturedBlocksProvider
    {
        private ConcurrentChain chain;
        private IDepositExtractor depositExtractor;
        private IBlockRepository blockRepository;
        private Dictionary<uint256, Block> blockCache;

        public MaturedBlocksProvider(ILoggerFactory loggerFactory,
            ConcurrentChain chain, IDepositExtractor depositExtractor, IBlockRepository blockRepository)
        {
            this.chain = chain;
            this.depositExtractor = depositExtractor;
            this.blockRepository = blockRepository;
            this.blockCache = new Dictionary<uint256, Block>();
        }

        /// <summary>
        /// Gets all the available chained headers starting at the specified block height up to a maximum number of headers.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start.</param>
        /// <param name="maxHeaders">The maximum number of headers to get.</param>
        /// <returns>All the available chained headers starting at the specified block height up to the maximum number of headers.</returns>
        private async Task<List<ChainedHeader>> GetChainedHeadersAsync(int blockHeight, int maxHeaders)
        {
            // Pre-load the block data
            var blockHashes = new List<uint256>();
            var chainedHeaders = new List<ChainedHeader>();
            for (; blockHeight <= this.chain.Tip.Height; blockHeight++)
            {
                if (maxHeaders-- <= 0)
                    break;

                ChainedHeader chainedHeader = this.chain.GetBlock(blockHeight);

                if (chainedHeader == null)
                    break;

                chainedHeaders.Add(chainedHeader);
                blockHashes.Add(chainedHeader.HashBlock);
            }

            List<Block> blocks = await this.blockRepository.GetBlocksAsync(blockHashes).ConfigureAwait(false);
            for (int index = 0; index < blockHashes.Count; index++)
            {
                if (blocks[index] == null)
                {
                    return chainedHeaders.GetRange(0, index).ToList();
                }

                chainedHeaders[index].Block = blocks[index];
            }

            return chainedHeaders;
        }

        /// <inheritdoc />
        public async Task<List<IMaturedBlockDeposits>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks)
        {
            int matureHeight = (this.chain.Tip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (blockHeight > matureHeight)
            {
                throw new InvalidOperationException($"Block height {blockHeight} submitted is not mature enough. Blocks less than a height of {matureHeight} can be processed.");
            }

            List<ChainedHeader> chainedHeaders = await this.GetChainedHeadersAsync(blockHeight, Math.Min(maxBlocks, matureHeight - blockHeight + 1));

            if (chainedHeaders.Count == 0)
            {
                throw new InvalidOperationException($"Block with height {blockHeight} was not found on the block chain.");
            }

            var maturedBlocks = new List<IMaturedBlockDeposits>();

            for (int index = 0; index < chainedHeaders.Count; index++)
            {
                IMaturedBlockDeposits maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(chainedHeaders[index]);

                if (maturedBlockDeposits == null)
                {
                    throw new InvalidOperationException($"Unable to get deposits for block at height {chainedHeaders[index].Height}");
                }

                maturedBlocks.Add(maturedBlockDeposits);
            }

            return maturedBlocks;
        }

        private ChainedHeader GetNewlyMaturedBlock(ChainedHeader chainedHeader)
        {
            var newMaturedHeight = chainedHeader.Height - (int)this.depositExtractor.MinimumDepositConfirmations;

            if (newMaturedHeight < 0) return null;

            ChainedHeader newMaturedBlock = this.chain.GetBlock(newMaturedHeight);

            if (newMaturedBlock.Block != null)
                return newMaturedBlock;

            if (!this.blockCache.TryGetValue(newMaturedBlock.HashBlock, out Block block))
            {
                List<ChainedHeader> chainedHeaders = this.GetChainedHeadersAsync(newMaturedHeight, (int)this.depositExtractor.MinimumDepositConfirmations).GetAwaiter().GetResult();
                foreach (ChainedHeader header in chainedHeaders)
                {
                    this.blockCache[header.HashBlock] = header.Block;
                }

                if (chainedHeaders.Count < 1)
                    return null;

                block = chainedHeaders[0].Block;
            }

            newMaturedBlock.Block = block;

            this.blockCache.Remove(newMaturedBlock.HashBlock);

            return newMaturedBlock;
        }

        /// <inheritdoc />
        public IMaturedBlockDeposits ExtractMaturedBlockDeposits(ChainedHeader chainedHeader)
        {
            return this.depositExtractor.ExtractBlockDeposits(this.GetNewlyMaturedBlock(chainedHeader));
        }
    }
}
