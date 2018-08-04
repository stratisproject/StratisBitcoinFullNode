using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="BitcoinMain"/> network block's header has a valid block version.
    /// <seealso cref="BitcoinActivationRule" />
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class BitcoinHeaderVersionRule : HeaderVersionRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated or otherwise invalid.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;

            // BIP9 mandates that the top bits of version be 001. So a standard node should never generate
            // block versions between 4 and 0x20000000. Block versions 5 onwards were never allocated, as the
            // BIP9 standard became predominant.
            if ((chainedHeader.Header.Version > 4) && (chainedHeader.Header.Version < ThresholdConditionCache.VersionbitsTopBits))
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            return Task.CompletedTask;
        }
    }
}