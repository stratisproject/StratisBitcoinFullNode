using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Check that the previous block hash is correct.
    /// </summary>  
    [ValidationRule(CanSkipValidation = false)]
    public class BlockHeaderRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidPrevTip">The tip is invalid because a reorg has been detected.</exception>
        public override Task RunAsync(RuleContext context)
        {
            // Check that the current block has not been reorged.
            // Catching a reorg at this point will not require a rewind.
            if (context.BlockValidationContext.Block.Header.HashPrevBlock != context.ConsensusTip.HashBlock)
            {
                this.Logger.LogTrace("Reorganization detected.");
                ConsensusErrors.InvalidPrevTip.Throw();
            }

            this.Logger.LogTrace("Validating new block.");

            // Build the next block in the chain of headers. The chain header is most likely already created by
            // one of the peers so after we create a new chained block (mainly for validation)
            // we ask the chain headers for its version (also to prevent memory leaks).
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, 
                context.BlockValidationContext.Block.Header.GetHash(), 
                context.ConsensusTip);

            // Liberate from memory the block created above if possible.
            context.BlockValidationContext.ChainedBlock = this.Parent.Chain.GetBlock(context.BlockValidationContext.ChainedBlock.HashBlock) ?? context.BlockValidationContext.ChainedBlock;
            context.SetBestBlock(this.Parent.DateTimeProvider.GetTimeOffset());

            // Calculate the consensus flags and check they are valid.
            context.Flags = this.Parent.NodeDeployments.GetFlags(context.BlockValidationContext.ChainedBlock);

            return Task.CompletedTask;
        }
    }
}