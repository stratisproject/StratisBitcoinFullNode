using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Set the <see cref="RuleContext.Flags"/> property that defines what deployments have been activated.
    /// </summary>
    /// <remarks>This is partial AND full validation rule.</remarks>
    public class SetActivationDeploymentsRule : AsyncConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // Calculate the consensus flags and check they are valid.
            context.Flags = this.Parent.NodeDeployments.GetFlags(context.ValidationContext.ChainTipToExtend);

            return Task.CompletedTask;
        }
    }
}