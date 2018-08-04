using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="BitcoinMain"/> network block's header has a valid block version.
    /// <seealso cref="BitcoinActivationRule" />
    /// </summary>
    [HeaderValidationRule]
    public class BitcoinHeaderVersionRule : HeaderVersionRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated or otherwise invalid.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // This is a stub rule - all version numbers are valid except those rejected by BitcoinActivationRule based
            // on the combination of their block height and version number.

            // All networks need a HeaderVersionRule implementation, as ComputeBlockVersion is used for block creation.
            
            return Task.CompletedTask;
        }
    }
}