using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
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
        /// Retrieves deposits for the indicated blocks from the block repository.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="maxBlocks">The number of blocks to retrieve.</param>
        /// <param name="maxDeposits">The number of deposits to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the blocks are not mature or not found.</exception>
        Task<Result<List<MaturedBlockDepositsModel>>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks, int maxDeposits = int.MaxValue);
    }

    public sealed class MaturedBlocksProvider : IMaturedBlocksProvider
    {
        public const string RetrieveBlockHeightHigherThanMaturedTipMessage = "The submitted block height of {0} is not mature enough. Blocks below {1} can be returned.";

        public const string UnableToRetrieveBlockDataFromConsensusMessage = "Stopping mature block collection and sending what we've collected. Reason: Unable to get block data for {0} from consensus.";

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
        public async Task<Result<List<MaturedBlockDepositsModel>>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks, int maxDeposits = int.MaxValue)
        {
            ChainedHeader consensusTip = this.consensusManager.Tip;

            if (consensusTip == null)
            {
                return Result<List<MaturedBlockDepositsModel>>.Fail("Not ready to provide blocks.");
            }

            int matureTipHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (retrieveFromBlockHeight > maturedTipBlockHeight)
            {
                this.logger.LogTrace("(-)[RETRIEVEFROMBLOCK_HIGHER_THAN_MATUREDTIP]:{0}={1},{2}={3}", nameof(retrieveFromBlockHeight), retrieveFromBlockHeight, nameof(maturedTipBlockHeight), maturedTipBlockHeight);
                return SerializableResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(RetrieveBlockHeightHigherThanMaturedTipMessage, retrieveFromBlockHeight, maturedTipBlockHeight));
            }

            var maturedBlockDepositModels = new List<MaturedBlockDepositsModel>();

            // Half of the timeout. We will also need time to convert it to json.
            int maxTimeCollectionCanTakeMs = RestApiClientBase.TimeoutMs / 2;
            var cancellation = new CancellationTokenSource(maxTimeCollectionCanTakeMs);

            int maxBlockHeight = Math.Min(matureTipHeight, blockHeight + maxBlocks - 1);

            var headers = new List<ChainedHeader>();
            ChainedHeader header = consensusTip.GetAncestor(maxBlockHeight);
            for (int i = maxBlockHeight; i >= blockHeight; i--)
            {
                headers.Add(header);
                header = header.Previous;
            }

            headers.Reverse();

            int numDeposits = 0;

            for (int ndx = 0; ndx < headers.Count; ndx += 100)
            {
                List<ChainedHeader> currentHeaders = headers.GetRange(ndx, Math.Min(100, headers.Count - ndx));

                List<uint256> hashes = currentHeaders.Select(h => h.HashBlock).ToList();

                ChainedHeaderBlock[] blocks = this.consensusManager.GetBlockData(hashes);

                foreach (ChainedHeaderBlock chainedHeaderBlock in blocks)
                {
                    if (chainedHeaderBlock?.Block?.Transactions == null)
                    {
                        this.logger.LogDebug("Unexpected null data. Send what we've collected.");

                        return Result<List<MaturedBlockDepositsModel>>.Ok(maturedBlocks);
                    }

                    MaturedBlockDepositsModel maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(chainedHeaderBlock);
                    
                    maturedBlocks.Add(maturedBlockDeposits);

                    numDeposits += maturedBlockDeposits.Deposits?.Count ?? 0;

                    if (maturedBlocks.Count >= maxBlocks || numDeposits >= maxDeposits)
                        return Result<List<MaturedBlockDepositsModel>>.Ok(maturedBlocks);

                    if (cancellation.IsCancellationRequested)
                    {
                        this.logger.LogDebug("Stop matured blocks collection because it's taking too long. Send what we've collected.");

                        return Result<List<MaturedBlockDepositsModel>>.Ok(maturedBlocks);
                    }
                }
            }

            return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
        }
    }
}