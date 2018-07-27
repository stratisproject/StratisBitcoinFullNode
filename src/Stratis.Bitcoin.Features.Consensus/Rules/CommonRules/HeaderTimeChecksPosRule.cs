using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="PosBlock"/> timestamp is greater than previous block timestamp.
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class HeaderTimeChecksPosRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BlockTimestampTooEarly">Thrown if block time is equal or behind the previous block.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;

            // Check timestamp against prev.
            if (chainedHeader.Header.Time <= chainedHeader.Previous.Header.Time)
            {
                this.Logger.LogTrace("(-)[TIME_TOO_EARLY]");
                ConsensusErrors.BlockTimestampTooEarly.Throw();
            }

            return Task.CompletedTask;
        }
    }
}