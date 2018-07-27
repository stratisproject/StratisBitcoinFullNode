using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="StratisMain"/> network block's header has a valid block version.
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class StratisHeaderVersionRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;

            if (chainedHeader.Header.Version < 7)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            return Task.CompletedTask;
        }
    }
}