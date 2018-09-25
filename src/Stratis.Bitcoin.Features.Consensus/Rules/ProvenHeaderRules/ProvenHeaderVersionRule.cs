using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Checks if <see cref="StratisMain" /> network proven block's header has a valid block version.
    /// POS proven header users version 8.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules.ProvenHeaderValidationConsensusRule" />
    public class ProvenHeaderVersionRule : ProvenHeaderValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if proven block's header version is outdated or otherwise invalid.</exception>
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            if (context.SkipValidation || !this.IsProvenHeaderActivated(context))
                return;

            if (!this.IsProvenHeader(context))
                return;

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            
            if (chainedHeader.Header.Version < 8)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.ProvenHeaderVersion.Throw();
            }
        }
    }
}