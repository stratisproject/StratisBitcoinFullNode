using System;
using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Set the <see cref="RuleContext.Flags"/> property that defines what deployments have been activated.</summary>
    public class SetActivationDeploymentsPartialValidationRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // Calculate the consensus flags and check they are valid.
            context.Flags = this.Parent.NodeDeployments.GetFlags(context.ValidationContext.ChainedHeaderToValidate);

            return Task.CompletedTask;
        }
    }

    //TODO merge those 2 classes into 1 after activation
    /// <summary>Set the <see cref="RuleContext.Flags"/> property that defines what deployments have been activated.</summary>
    public class SetActivationDeploymentsFullValidationRule : FullValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // Calculate the consensus flags and check they are valid.
            context.Flags = this.Parent.NodeDeployments.GetFlags(context.ValidationContext.ChainedHeaderToValidate);

            return Task.CompletedTask;
        }
    }
}