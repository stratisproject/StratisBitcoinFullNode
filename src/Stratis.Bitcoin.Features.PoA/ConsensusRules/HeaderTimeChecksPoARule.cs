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
        private PoANetwork network;

        private SlotsManager slotsManager;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            this.network = this.Parent.Network as PoANetwork;
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

            // Timestamp shouldn't be more than targetSpacing seconds in the future.
            if (chainedHeader.Header.Time > (this.Parent.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp() + this.network.TargetSpacingSeconds))
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
