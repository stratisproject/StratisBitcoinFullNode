using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// A rule that verifies checkpoints, this rules depends on per network override classes.
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class CheckpointsRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.CheckpointViolation">The block header hash does not match the expected checkpoint value.</exception>
        public override Task RunAsync(RuleContext context)
        {
            int height = context.ConsensusTipHeight + 1;
            BlockHeader header = context.ValidationContext.Block.Header;

            // Check that the block header hash matches the known checkpointed value, if any.
            if (!this.Parent.Checkpoints.CheckHardened(height, header.GetHash()))
            {
                this.Logger.LogTrace("(-)[CHECKPOINT_VIOLATION]");
                ConsensusErrors.CheckpointViolation.Throw();
            }

            // Check whether to use checkpoint to skip block validation.
            context.SkipValidation = false;
            if (this.Parent.ConsensusSettings.UseCheckpoints)
            {
                int lastCheckpointHeight = this.Parent.Checkpoints.GetLastCheckpointHeight();
                context.SkipValidation = context.ValidationContext.ChainedHeader.Height <= lastCheckpointHeight;
                if (context.SkipValidation)
                    this.Logger.LogTrace("Block validation will be partially skipped due to block height {0} is not greater than last checkpointed block height {1}.", context.ValidationContext.ChainedHeader.Height, lastCheckpointHeight);
            }

            return Task.CompletedTask;
        }
    }
}
