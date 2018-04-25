using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header.
    /// </summary>
    public class BlockHeaderPosContextualRule : StakeStoreConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block' timestamp too far in the future.</exception>
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        /// <exception cref="ConsensusErrors.BlockTimestampTooEarly"> Thrown if the block timestamp is before the previous block timestamp.</exception>
        /// <exception cref="ConsensusErrors.StakeTimeViolation">Thrown if the coinstake timestamp is invalid.</exception>
        /// <exception cref="ConsensusErrors.ProofOfWorkTooHigh">The block's height is higher than the last allowed PoW block.</exception>
        public override Task RunAsync(RuleContext context)
        {
            ChainedBlock chainedBlock = context.BlockValidationContext.ChainedBlock;
            this.Logger.LogTrace("Height of block is {0}, block timestamp is {1}, previous block timestamp is {2}, block version is 0x{3:x}.", chainedBlock.Height, chainedBlock.Header.Time, chainedBlock.Previous.Header.Time, chainedBlock.Header.Version);

            if (chainedBlock.Header.Version < 7)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            if (context.Stake.BlockStake.IsProofOfWork() && (chainedBlock.Height > this.Parent.ConsensusParams.LastPOWBlock))
            {
                this.Logger.LogTrace("(-)[POW_TOO_HIGH]");
                ConsensusErrors.ProofOfWorkTooHigh.Throw();
            }

            // Check coinbase timestamp.
            if (chainedBlock.Header.Time > this.FutureDrift(context.BlockValidationContext.Block.Transactions[0].Time))
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Check coinstake timestamp.
            if (context.Stake.BlockStake.IsProofOfStake()
                && !this.CheckCoinStakeTimestamp(chainedBlock.Header.Time, context.BlockValidationContext.Block.Transactions[1].Time))
            {
                this.Logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
            }

            // Check timestamp against prev.
            if (chainedBlock.Header.Time <= chainedBlock.Previous.Header.Time)
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
            return (blockTime == transactionTime) && ((transactionTime & PosConsensusValidator.StakeTimestampMask) == 0);
        }

        /// <summary>
        /// Applies future drift to provided timestamp.
        /// </summary>
        /// <remarks>
        /// Future drift is maximal allowed block's timestamp difference over adjusted time.
        /// If this difference is greater block won't be accepted.
        /// </remarks>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns>Timestamp with maximum future drift applied.</returns>
        private long FutureDrift(long time)
        {
            return this.IsDriftReduced(time) ? time + 15 : time + 128 * 60 * 60;
        }

        /// <summary>
        /// Checks whether the future drift should be reduced after provided timestamp.
        /// </summary>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns><c>true</c> if for this timestamp future drift should be reduced, <c>false</c> otherwise.</returns>
        private bool IsDriftReduced(long time)
        {
            return time > PosConsensusValidator.DriftingBugFixTimestamp;
        }
    }
}