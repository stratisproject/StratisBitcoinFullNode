using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header.
    /// </summary>
    public class PosTimeMaskRule : PartialValidationConsensusRule
    {
        public PosFutureDriftRule FutureDriftRule { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            this.FutureDriftRule = this.Parent.GetRule<PosFutureDriftRule>();
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block' timestamp too far in the future.</exception>
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        /// <exception cref="ConsensusErrors.BlockTimestampTooEarly"> Thrown if the block timestamp is before the previous block timestamp.</exception>
        /// <exception cref="ConsensusErrors.StakeTimeViolation">Thrown if the coinstake timestamp is invalid.</exception>
        /// <exception cref="ConsensusErrors.ProofOfWorkTooHigh">The block's height is higher than the last allowed PoW block.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            this.Logger.LogTrace("Height of block is {0}, block timestamp is {1}, previous block timestamp is {2}, block version is 0x{3:x}.", chainedHeader.Height, chainedHeader.Header.Time, chainedHeader.Previous?.Header.Time, chainedHeader.Header.Version);

            var posRuleContext = context as PosRuleContext;
            posRuleContext.BlockStake = BlockStake.Load(context.ValidationContext.BlockToValidate);

            if (posRuleContext.BlockStake.IsProofOfWork() && (chainedHeader.Height > this.Parent.ConsensusParams.LastPOWBlock))
            {
                this.Logger.LogTrace("(-)[POW_TOO_HIGH]");
                ConsensusErrors.ProofOfWorkTooHigh.Throw();
            }

            // Check coinbase timestamp.
            uint coinbaseTime = context.ValidationContext.BlockToValidate.Transactions[0].Time;
            if (chainedHeader.Header.Time > coinbaseTime + this.FutureDriftRule.GetFutureDrift(coinbaseTime))
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Check coinstake timestamp.
            if (posRuleContext.BlockStake.IsProofOfStake()
                && !this.CheckCoinStakeTimestamp(chainedHeader.Header.Time, context.ValidationContext.BlockToValidate.Transactions[1].Time))
            {
                this.Logger.LogTrace("(-)[BAD_TIME]");
                ConsensusErrors.StakeTimeViolation.Throw();
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
            return (blockTime == transactionTime) && ((transactionTime & PosConsensusOptions.StakeTimestampMask) == 0);
        }
    }
}