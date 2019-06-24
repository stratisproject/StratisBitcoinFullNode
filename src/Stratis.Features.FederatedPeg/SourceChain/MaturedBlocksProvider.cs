using System.Collections.Generic;
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
        /// <param name="retrieveFromBlockHeight">The block height at which to start retrieving blocks from.</param>
        /// <param name="amountOfBlocksToRetrieve">The number of blocks to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        SerializableResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int retrieveFromBlockHeight, int amountOfBlocksToRetrieve);
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
        public SerializableResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int retrieveFromBlockHeight, int amountOfBlocksToRetrieve)
        {
            ChainedHeader consensusTip = this.consensusManager.Tip;

            int maturedTipBlockHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (retrieveFromBlockHeight > maturedTipBlockHeight)
            {
                this.logger.LogTrace("(-)[RETRIEVEFROMBLOCK_HIGHER_THAN_MATUREDTIP]:{0}={1},{2}={3}", nameof(retrieveFromBlockHeight), retrieveFromBlockHeight, nameof(maturedTipBlockHeight), maturedTipBlockHeight);
                return SerializableResult<List<MaturedBlockDepositsModel>>.Fail(string.Format(RetrieveBlockHeightHigherThanMaturedTipMessage, retrieveFromBlockHeight, maturedTipBlockHeight));
            }

            var maturedBlockDepositModels = new List<MaturedBlockDepositsModel>();

            // Half of the timeout. We will also need time to convert it to json.
            int maxTimeCollectionCanTakeMs = RestApiClientBase.TimeoutMs / 2;
            var cancellation = new CancellationTokenSource(maxTimeCollectionCanTakeMs);

            for (int i = retrieveFromBlockHeight; (i <= maturedTipBlockHeight) && (i < retrieveFromBlockHeight + amountOfBlocksToRetrieve); i++)
            {
                ChainedHeader currentHeader = consensusTip.GetAncestor(i);

                ChainedHeaderBlock block = this.consensusManager.GetBlockData(currentHeader.HashBlock);

                if (block?.Block?.Transactions == null)
                {
                    this.logger.LogDebug(UnableToRetrieveBlockDataFromConsensusMessage, currentHeader);
                    this.logger.LogTrace("(-)[BLOCKDATA_MISSING_FROM_CONSENSUS]");
                    return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels, string.Format(UnableToRetrieveBlockDataFromConsensusMessage, currentHeader));
                }

                MaturedBlockDepositsModel maturedBlockDepositModel = this.depositExtractor.ExtractBlockDeposits(block);

                if (maturedBlockDepositModel.Deposits != null && maturedBlockDepositModel.Deposits.Count > 0)
                    this.logger.LogDebug("{0} deposits extracted at block {1}", maturedBlockDepositModel.Deposits.Count, currentHeader);

                maturedBlockDepositModels.Add(maturedBlockDepositModel);

                if (cancellation.IsCancellationRequested && maturedBlockDepositModels.Count > 0)
                {
                    this.logger.LogDebug("Stopping mature block collection and sending what has been collected. Reason: The operation took longer than {0} ms.", maxTimeCollectionCanTakeMs);
                    break;
                }
            }

            return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
        }
    }
}