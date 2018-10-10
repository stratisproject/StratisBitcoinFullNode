using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
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
            if (chainedHeader.Header.Time > (context.Time + TimeSpan.FromSeconds(this.network.TargetSpacingSeconds)).ToUnixTimeSeconds())
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            if (!this.slotsManager.IsValidTimestamp(chainedHeader.Header.Time))
            {
                this.Logger.LogTrace("(-)[INVALID_TIMESTAMP]");
                PoAConsensusErrors.InvalidHeaderTimestamp.Throw();
            }
        }
    }
}
