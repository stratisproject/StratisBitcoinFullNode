using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
{
    /// <summary>
    /// Ensures that timestamp of current block is greater than timestamp of previous block,
    /// that timestamp is not more than targetSpacing seconds far in the future and that it is devisible by target spacing.
    /// </summary>
    /// <seealso cref="HeaderValidationConsensusRule" />
    public class HeaderTimeChecksPoARule : HeaderValidationConsensusRule
    {
        /// <summary>Up to how many seconds headers's timestamp can be in the future to be considered valid.</summary>
        public const int MaxFutureDriftSeconds = 10;

        private ISlotsManager slotsManager;

        /// <inheritdoc />
        [NoTrace]
        public override void Initialize()
        {
            base.Initialize();

            this.slotsManager = (this.Parent as PoAConsensusRuleEngine).SlotsManager;
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
                this.Logger.LogWarning("Peer presented header with timestamp that is too far in to the future. Header was ignored." +
                                       " If you see this message a lot consider checking if your computer's time is correct.");
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
