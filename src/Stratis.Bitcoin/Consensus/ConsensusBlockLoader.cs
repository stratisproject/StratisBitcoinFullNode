using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Shared block loading logic used by <see cref="ConsensusManager"/>.
    /// </summary>
    public sealed class ConsensusBlockLoader
    {
        private readonly ILogger logger;

        public ConsensusBlockLoader(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <summary>Loads the block data from <see cref="ChainedHeaderTree"/> or block store if it's enabled.</summary>
        public async Task<ChainedHeaderBlock> LoadBlockDataAsync(ConsensusManager consensusManager, uint256 blockHash)
        {
            this.logger.LogTrace("({0}:{1})", nameof(blockHash), blockHash);

            ChainedHeaderBlock chainedHeaderBlock;

            lock (consensusManager.PeerLock)
            {
                chainedHeaderBlock = consensusManager.ChainedHeaderTree.GetChainedHeaderBlock(blockHash);
            }

            if (chainedHeaderBlock == null)
            {
                this.logger.LogTrace("Block hash '{0}' is not part of the tree.", blockHash);
                this.logger.LogTrace("(-)[INVALID_HASH]:null");
                return null;
            }

            if (chainedHeaderBlock.Block != null)
            {
                this.logger.LogTrace("Block pair '{0}' was found in memory.", chainedHeaderBlock);

                this.logger.LogTrace("(-)[FOUND_IN_CHT]:'{0}'", chainedHeaderBlock);
                return chainedHeaderBlock;
            }

            if (consensusManager.BlockStore != null)
            {
                Block block = await consensusManager.BlockStore.GetBlockAsync(blockHash).ConfigureAwait(false);
                if (block != null)
                {
                    var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                    this.logger.LogTrace("(-)[FOUND_IN_BLOCK_STORE]:'{0}'", newBlockPair);
                    return newBlockPair;
                }
            }

            this.logger.LogTrace("(-)[NOT_FOUND]:'{0}'", chainedHeaderBlock);
            return chainedHeaderBlock;
        }
    }
}
