using System;
using System.Collections.Generic;
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

        public MaturedBlocksProvider(ILoggerFactory loggerFactory,
            ConcurrentChain chain, IDepositExtractor depositExtractor, IBlockRepository blockRepository)
        {
            this.chain = chain;
            this.depositExtractor = depositExtractor;
            this.blockRepository = blockRepository;
        }

        /// <inheritdoc />
        public async Task<List<IMaturedBlockDeposits>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks)
        {
            int currentHeight = blockHeight;
            int matureHeight = (this.chain.Tip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (currentHeight > matureHeight)
            {
                throw new InvalidOperationException($"Block height {currentHeight} submitted is not mature enough. Blocks less than a height of {matureHeight} can be processed.");
            }

            // Pre-load the block data
            var blockHashes = new List<uint256>();
            var chainedHeaders = new ChainedHeader[Math.Min(maxBlocks, matureHeight - currentHeight)];
            for (int index = 0; index < chainedHeaders.Length; index++, currentHeight++)
            {
                if (maxBlocks-- <= 0)
                    break;

                if (currentHeight >= this.chain.Tip.Height)
                    break;

                chainedHeaders[index] = this.chain.GetBlock(currentHeight);

                if (chainedHeaders[index] == null)
                {
                    if (currentHeight == blockHeight)
                    {
                        throw new InvalidOperationException($"Block with height {currentHeight} was not found on the block chain.");
                    }
                    else
                    {
                        break;
                    }
                }

                blockHashes.Add(chainedHeaders[index].HashBlock);
            }

            List<Block> blocks = await this.blockRepository.GetBlocksAsync(blockHashes).ConfigureAwait(false);
            var maturedBlocks = new List<IMaturedBlockDeposits>();

            for (int index = 0; index < blocks.Count; index++)
            {
                if (blocks[index] == null)
                    break;

                chainedHeaders[index].Block = blocks[index];

                IMaturedBlockDeposits maturedBlockDeposits = this.depositExtractor.ExtractMaturedBlockDeposits(chainedHeaders[index]);

                if (maturedBlockDeposits == null)
                {
                    throw new InvalidOperationException($"Unable to get deposits for block at height {chainedHeaders[index].Height}");
                }

                maturedBlocks.Add(maturedBlockDeposits);
            }

            return maturedBlocks;
        }
    }
}
