﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header.
    /// </summary>
    public class BlockHeaderPosContextualRule : StakeStoreConsensusRule
    {
        /// <summary>PoS block's timestamp mask.</summary>
        /// <remarks>Used to decrease granularity of timestamp. Supposed to be 2^n-1.</remarks>
        public const uint StakeTimestampMask = 0x0000000F;

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block' timestamp too far in the future.</exception>
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        /// <exception cref="ConsensusErrors.BlockTimestampTooEarly"> Thrown if the block timestamp is before the previous block timestamp.</exception>
        /// <exception cref="ConsensusErrors.StakeTimeViolation">Thrown if the coinstake timestamp is invalid.</exception>
        /// <exception cref="ConsensusErrors.ProofOfWorkTooHigh">The block's height is higher than the last allowed PoW block.</exception>
        public override Task RunAsync(RuleContext context)
        {
            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;
            this.Logger.LogTrace("Height of block is {0}, block timestamp is {1}, previous block timestamp is {2}, block version is 0x{3:x}.", chainedHeader.Height, chainedHeader.Header.Time, chainedHeader.Previous.Header.Time, chainedHeader.Header.Version);

            PosRuleContext posRuleContext = context as PosRuleContext;

            if (chainedHeader.Header.Version < 7)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            if (posRuleContext.BlockStake.IsProofOfWork() && (chainedHeader.Height > this.Parent.ConsensusParams.LastPOWBlock))
            {
                this.Logger.LogTrace("(-)[POW_TOO_HIGH]");
                ConsensusErrors.ProofOfWorkTooHigh.Throw();
            }

            // Check coinbase timestamp.
            uint coinbaseTime = context.ValidationContext.Block.Transactions[0].Time;
            if (chainedHeader.Header.Time > coinbaseTime + PosFutureDriftRule.GetFutureDrift(coinbaseTime))
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Check coinstake timestamp.
            if (posRuleContext.BlockStake.IsProofOfStake()
                && !this.CheckCoinStakeTimestamp(chainedHeader.Header.Time, context.ValidationContext.Block.Transactions[1].Time))
            {
                this.Logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Check timestamp against prev.
            if (chainedHeader.Header.Time <= chainedHeader.Previous.Header.Time)
            {
                this.Logger.LogTrace("(-)[TIME_TOO_EARLY]");
                ConsensusErrors.BlockTimestampTooEarly.Throw();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks whether the coinstake timestamp meets protocol.
        /// </summary>
        /// <param name="blockTime">The block time.</param>
        /// <param name="transactionTime">Transaction UNIX timestamp.</param>
        /// <returns><c>true</c> if block timestamp is equal to transaction timestamp, <c>false</c> otherwise.</returns>
        private bool CheckCoinStakeTimestamp(long blockTime, long transactionTime)
        {
            return (blockTime == transactionTime) && ((transactionTime & StakeTimestampMask) == 0);
        }
    }
}