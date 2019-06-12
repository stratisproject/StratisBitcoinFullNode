﻿using System.Collections.Generic;
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
        Result<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int blockHeight, int maxBlocks);
    }

    public sealed class MaturedBlocksProvider : IMaturedBlocksProvider
    {
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
        public Result<List<MaturedBlockDepositsModel>> GetMaturedDeposits(int blockHeight, int maxBlocks)
        {
            ChainedHeader consensusTip = this.consensusManager.Tip;

            int matureTipHeight = (consensusTip.Height - (int)this.depositExtractor.MinimumDepositConfirmations);

            if (blockHeight > matureTipHeight)
            {
                // We need to return a Result type here to explicitly indicate failure and the reason for failure.
                // This is an expected condition so we can avoid throwing an exception here.
                return Result<List<MaturedBlockDepositsModel>>.Fail($"Block height {blockHeight} submitted is not mature enough, only blocks at a height less than {matureTipHeight} can be processed.");
            }

            var maturedBlocks = new List<MaturedBlockDepositsModel>();

            // Half of the timeout. We will also need time to convert it to json.
            int maxTimeCollectionCanTakeMs = RestApiClientBase.TimeoutMs / 2;
            var cancellation = new CancellationTokenSource(maxTimeCollectionCanTakeMs);

            for (int i = blockHeight; (i <= matureTipHeight) && (i < blockHeight + maxBlocks); i++)
            {
                ChainedHeader currentHeader = consensusTip.GetAncestor(i);

                ChainedHeaderBlock block = this.consensusManager.GetBlockData(currentHeader.HashBlock);

                if (block?.Block?.Transactions == null)
                {
                    // Report unexpected results from consenus manager.
                    this.logger.LogWarning("Matured blocks collection stopped at {0} due to consensus manager integrity failure. Sending what has been collected up until this point.", currentHeader);
                    break;
                }

                MaturedBlockDepositsModel maturedBlockDeposits = this.depositExtractor.ExtractBlockDeposits(block);

                if (maturedBlockDeposits == null)
                    return Result<List<MaturedBlockDepositsModel>>.Fail($"Unable to get deposits for block at height {currentHeader.Height}");

                maturedBlocks.Add(maturedBlockDeposits);

                if (cancellation.IsCancellationRequested && maturedBlocks.Count > 0)
                {
                    this.logger.LogDebug("Stop matured blocks collection because it's taking too long, sending what has been collected.");
                    break;
                }
            }

            return Result<List<MaturedBlockDepositsModel>>.Ok(maturedBlocks);
        }
    }
}