using System;
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
        /// Retrieves deposits for the indicated blocks from the block repository and throws an error if the blocks are not mature enough.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="maxBlocks">The number of blocks to retrieve.</param>
        /// <param name="maxDeposits">The number of deposits to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        SerializableResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int blockHeight, int maxBlocks, int maxDeposits = int.MaxValue);
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
            this.depositExtractor = depositExtractor;
            this.consensusManager = consensusManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public SerializableResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int retrieveFromHeight, int maxBlocks, int maxDeposits = int.MaxValue)
        {
            ChainedHeader consensusTip = this.consensusManager.Tip;

            if (consensusTip == null)
            {
                return SerializableResult<List<MaturedBlockDepositsModel>>.Fail("Consensus is not ready to provide blocks.");
            }

            int matureTipHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (retrieveFromHeight > matureTipHeight)
            {
                this.logger.LogTrace("(-)[RETRIEVEFROMBLOCK_HIGHER_THAN_MATUREDTIP]:{0}={1},{2}={3}", nameof(retrieveFromHeight), retrieveFromHeight, nameof(matureTipHeight), matureTipHeight);
                return SerializableResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(RetrieveBlockHeightHigherThanMaturedTipMessage, retrieveFromHeight, matureTipHeight));
            }

            var maturedBlockDepositModels = new List<MaturedBlockDepositsModel>();

            // Half of the timeout. We will also need time to convert it to json.
            int maxTimeCollectionCanTakeMs = RestApiClientBase.TimeoutMs / 2;
            var cancellation = new CancellationTokenSource(maxTimeCollectionCanTakeMs);

            int maxBlockHeight = Math.Min(matureTipHeight, retrieveFromHeight + maxBlocks - 1);

            var headers = new List<ChainedHeader>();
            ChainedHeader header = consensusTip.GetAncestor(maxBlockHeight);
            for (int i = maxBlockHeight; i >= retrieveFromHeight; i--)
            {
                headers.Add(header);
                header = header.Previous;
            }

            headers.Reverse();

            int numberOfDeposits = 0;

            for (int headerIndex = 0; headerIndex < headers.Count; headerIndex += 100)
            {
                List<ChainedHeader> currentHeaders = headers.GetRange(headerIndex, Math.Min(100, headers.Count - headerIndex));

                var hashes = currentHeaders.Select(h => h.HashBlock).ToList();

                ChainedHeaderBlock[] blocks = this.consensusManager.GetBlockData(hashes);

                foreach (ChainedHeaderBlock chainedHeaderBlock in blocks)
                {
                    if (chainedHeaderBlock?.Block?.Transactions == null)
                    {
                        this.logger.LogDebug(UnableToRetrieveBlockDataFromConsensusMessage, chainedHeaderBlock.ChainedHeader);
                        this.logger.LogTrace("(-)[BLOCKDATA_MISSING_FROM_CONSENSUS]");
                        return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels, string.Format(UnableToRetrieveBlockDataFromConsensusMessage, chainedHeaderBlock.ChainedHeader));
                    }

                    MaturedBlockDepositsModel maturedBlockDepositModel = this.depositExtractor.ExtractBlockDeposits(chainedHeaderBlock);

                    if (maturedBlockDepositModel.Deposits != null && maturedBlockDepositModel.Deposits.Count > 0)
                        this.logger.LogDebug("{0} deposits extracted at block {1}", maturedBlockDepositModel.Deposits.Count, chainedHeaderBlock.ChainedHeader);

                    maturedBlockDepositModels.Add(maturedBlockDepositModel);

                    numberOfDeposits += maturedBlockDepositModel.Deposits?.Count ?? 0;

                    if (maturedBlockDepositModels.Count >= maxBlocks || numberOfDeposits >= maxDeposits)
                    {
                        this.logger.LogDebug("Stopping matured blocks collection, thresholds reached; {0}={1}, {2}={3}", nameof(maturedBlockDepositModels), maturedBlockDepositModels.Count, nameof(numberOfDeposits), numberOfDeposits);
                        return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
                    }

                    if (cancellation.IsCancellationRequested)
                    {
                        this.logger.LogDebug("Stopping matured blocks collection, the request is taking too long. Sending what has been collected.");

                        return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
                    }
                }
            }

            return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
        }
    }
}