using System;
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
        /// Retrieves deposits for the indicated blocks from the block repository and throws an error if the blocks are not mature enough.
        /// </summary>
        /// <param name="retrieveFromBlockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="amountOfBlocksToRetrieve">The number of blocks to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the blocks are not mature or not found.</exception>
        SerializableResult<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int retrieveFromBlockHeight, int amountOfBlocksToRetrieve);
    }

    public sealed class MaturedBlocksProvider : IMaturedBlocksProvider
    {
        public const string RetrieveBlockHeightHigherThanMaturedTipMessage = "The submitted block height of {0} is not mature enough. Blocks below {1} can be returned.";

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
                    // Report unexpected results from consenus manager.
                    this.logger.LogWarning("Stop matured blocks collection due to consensus manager integrity failure. Send what we've collected.");
                    break;
                }

                MaturedBlockDepositsModel maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(block);

                if (maturedBlockDeposits == null)
                    throw new InvalidOperationException($"Unable to get deposits for block at height {currentHeader.Height}");

                maturedBlockDepositModels.Add(maturedBlockDeposits);

                if (cancellation.IsCancellationRequested && maturedBlockDepositModels.Count > 0)
                {
                    this.logger.LogDebug("Stop matured blocks collection because it's taking too long. Send what we've collected.");
                    break;
                }
            }

            return SerializableResult<List<MaturedBlockDepositsModel>>.Ok(maturedBlockDepositModels);
        }
    }
}