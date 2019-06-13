using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Primitives;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public interface IMaturedBlocksProvider
    {
        /// <summary>
        /// Retrieves deposits for the indicated blocks from the block repository and throws an error if the blocks are not mature enough.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="maxBlocks">The number of blocks to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        ApiResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int blockHeight, int maxBlocks);
    }

    public sealed class MaturedBlocksProvider : IMaturedBlocksProvider
    {
        public const string BlockHeightNotMatureEnoughMessage = "The submitted block height of {0} is not mature enough, only blocks at a height less than {1} can be processed.";
        public const string NoMatureBlocksAtHeight = "No matured blocks found; {0}={1}, {2}={3}";
        public const string UnableToGetDepositsAtHeightMessage = "Unable to get deposits for block at height {0}.";

        private readonly IConsensusManager consensusManager;

        private readonly IDepositExtractor depositExtractor;

        private readonly ILogger logger;

        public MaturedBlocksProvider(IConsensusManager consensusManager, IDepositExtractor depositExtractor, ILoggerFactory loggerFactory)
        {
            this.consensusManager = consensusManager;
            this.depositExtractor = depositExtractor;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public ApiResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int startBlockHeight, int maxBlocks)
        {
            this.logger.LogTrace("{0}:{1}", nameof(startBlockHeight), startBlockHeight);

            ChainedHeader consensusTip = this.consensusManager.Tip;

            int matureTipHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (startBlockHeight > matureTipHeight)
            {
                this.logger.LogTrace("(-)[STARTBLOCK_HEIGHT_HIGHER_THAN_MATURETIP_HEIGHT]:{0}={1}", nameof(matureTipHeight), matureTipHeight);
                return ApiResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(BlockHeightNotMatureEnoughMessage, startBlockHeight, matureTipHeight));
            }

            var maturedBlocks = new List<MaturedBlockDepositsModel>();

            // Half of the timeout. We will also need time to convert it to json.
            int maxTimeCollectionCanTakeMs = RestApiClientBase.TimeoutMs / 2;
            var cancellation = new CancellationTokenSource(maxTimeCollectionCanTakeMs);

            for (int currentBlockHeight = startBlockHeight; (currentBlockHeight <= matureTipHeight) && (currentBlockHeight < startBlockHeight + maxBlocks); currentBlockHeight++)
            {
                ChainedHeader currentHeader = consensusTip.GetAncestor(currentBlockHeight);

                ChainedHeaderBlock chainedHeaderBlock = this.consensusManager.GetBlockData(currentHeader.HashBlock);

                if (chainedHeaderBlock == null)
                {
                    this.logger.LogWarning("Get block data for {0} returned null; stopping matured blocks collection and sending what has been collected up until this point.", currentHeader);
                    break;
                }

                if (chainedHeaderBlock.Block == null)
                {
                    this.logger.LogWarning("Get block data for {0} returned null block; stopping matured blocks collection and sending what has been collected up until this point.", currentHeader);
                    break;
                }

                if (chainedHeaderBlock.Block.Transactions == null)
                {
                    this.logger.LogWarning("Get block data for {0} returned null transactions on block; stopping matured blocks collection and sending what has been collected up until this point.", currentHeader);
                    break;
                }

                MaturedBlockDepositsModel maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(chainedHeaderBlock);

                if (maturedBlockDeposits == null)
                {
                    this.logger.LogTrace("(-)[NO_DEPOSITS_AT_HEIGHT]:{0}={1}", nameof(currentHeader.Height), currentHeader.Height);
                    return ApiResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(UnableToGetDepositsAtHeightMessage, currentHeader.Height));
                }

                maturedBlocks.Add(maturedBlockDeposits);

                if (cancellation.IsCancellationRequested && maturedBlocks.Count > 0)
                {
                    this.logger.LogDebug("Matured blocks collection has been cancelled because it is taking too long, sending what has been collected up until this point.");
                    break;
                }
            }

            if (maturedBlocks.Count == 0)
            {
                this.logger.LogTrace("(-)[MATUREDBLOCKS_EMPTY]");
                return ApiResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(NoMatureBlocksAtHeight, nameof(startBlockHeight), startBlockHeight, nameof(matureTipHeight), matureTipHeight));
            }

            return ApiResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlocks);
        }
    }
}