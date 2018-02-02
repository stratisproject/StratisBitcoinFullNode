﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header.
    /// </summary>
    public class BlockHeaderPowContextualRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash"> Thrown if block doesn't have a valid PoW header.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.BestBlock, nameof(context.BestBlock));

            BlockHeader header = context.BlockValidationContext.Block.Header;

            int height = context.BestBlock.Height + 1;

            // Check proof of work.
            if (header.Bits != context.NextWorkRequired)
            {
                this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }

            // Check timestamp against prev.
            if (header.BlockTime <= context.BestBlock.MedianTimePast)
            {
                this.Logger.LogTrace("(-)[TIME_TOO_OLD]");
                ConsensusErrors.TimeTooOld.Throw();
            }

            // Check timestamp.
            if (header.BlockTime > (context.Time + TimeSpan.FromHours(2)))
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Reject outdated version blocks when 95% (75% on testnet) of the network has upgraded:
            // check for version 2, 3 and 4 upgrades.
            if (((header.Version < 2) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34])) ||
                ((header.Version < 3) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66])) ||
                ((header.Version < 4) && (height >= this.Parent.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65])))
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            return Task.CompletedTask;
        }
    }
}