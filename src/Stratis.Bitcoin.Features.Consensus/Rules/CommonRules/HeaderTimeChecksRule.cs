using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Check time .
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class HeaderTimeChecksRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.TimeTooOld">Thrown if block's timestamp is too early.</exception>
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block' timestamp too far in the future.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            BlockHeader header = context.ValidationContext.Block.Header;

            // Check timestamp against prev.
            if (header.BlockTime <= context.ConsensusTip.GetMedianTimePast())
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

            return Task.CompletedTask;
        }
    }
}