using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// This rule is temporary until CM is activated.
    /// </summary>
    [HeaderValidationRule]
    [PartialValidationRule]
    [FullValidationRule]
    public class TemporarySetChainHeader : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        [Obsolete("This should be changed to a rule called SetDeploymentFlags")]
        public override Task RunAsync(RuleContext context)
        {
            if (context.ValidationContext.ChainedHeader == null)
            {
                // Check that the current block has not been reorged.
                // Catching a reorg at this point will not require a rewind.
                if (context.ValidationContext.Block.Header.HashPrevBlock != context.ConsensusTip.HashBlock)
                {
                    this.Logger.LogTrace("Reorganization detected.");
                    ConsensusErrors.InvalidPrevTip.Throw();
                }

                this.Logger.LogTrace("Validating new block.");

                // Build the next block in the chain of headers. The chain header is most likely already created by
                // one of the peers so after we create a new chained block (mainly for validation)
                // we ask the chain headers for its version (also to prevent memory leaks).
                context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header,
                    context.ValidationContext.Block.Header.GetHash(),
                    context.ConsensusTip);

                // Liberate from memory the block created above if possible.
                context.ValidationContext.ChainedHeader = this.Parent.Chain.GetBlock(context.ValidationContext.ChainedHeader.HashBlock) ?? context.ValidationContext.ChainedHeader;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Set the <see cref="RuleContext.Flags"/> property that defines what deployments have been activated.
    /// </summary>
    [PartialValidationRule]
    public class SetActivationDeploymentsRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // Calculate the consensus flags and check they are valid.
            context.Flags = this.Parent.NodeDeployments.GetFlags(context.ValidationContext.ChainedHeader);

            return Task.CompletedTask;
        }
    }
}