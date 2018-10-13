using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    /// <summary>
    /// Ensures that timestamp of current block is greater than timestamp of previous block,
    /// that timestamp is not more than targetSpacing seconds far in the future and that it is devisible by target spacing.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.HeaderValidationConsensusRule" />
    public class HeaderTimeChecksPoARule : HeaderValidationConsensusRule
    {
        public const int MaxFutureDriftSeconds = 60;

        private readonly SlotsManager slotsManager;

        public HeaderTimeChecksPoARule(SlotsManager slotsManager)
        {
            this.slotsManager = slotsManager;
        }

        /// <inheritdoc />
        public override void Run(RuleContext context)
        {
            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            // Timestamp should be greater than timestamp of prev block.
            if (chainedHeader.Header.Time <= chainedHeader.Previous.Header.Time)
            {
                this.Logger.LogTrace("(-)[TIME_TOO_OLD]");
                ConsensusErrors.TimeTooOld.Throw();
            }

            // Timestamp shouldn't be more than current time plus max future drift.
            long maxValidTime = this.Parent.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp() + MaxFutureDriftSeconds;
            if (chainedHeader.Header.Time > maxValidTime)
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Timestamp should be divisible by target spacing.
            if (!this.slotsManager.IsValidTimestamp(chainedHeader.Header.Time))
            {
                this.Logger.LogTrace("(-)[INVALID_TIMESTAMP]");
                PoAConsensusErrors.InvalidHeaderTimestamp.Throw();
            }
        }
    }
}
