using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Checks if <see cref="Block"/> the client is supported by the network.</summary>
    public class ClientVersionLimitRule : HeaderValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.ClientVersionTooOld">Thrown if client version is too old.</exception>
        public override void Run(RuleContext context)
        {
            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            int maxSupportedSyncBlockHeight = this.Parent.Network.Consensus.Options.MaxSupportedSyncBlockHeight;

            // Check if the client version is supported by the network
            if ((maxSupportedSyncBlockHeight > 0) && (chainedHeader.Height > maxSupportedSyncBlockHeight))
            {
                this.Logger.LogError("(-)[CLIENT_TOO_OLD]");
                ConsensusErrors.ClientVersionTooOld.Throw();
            }
        }
    }
}