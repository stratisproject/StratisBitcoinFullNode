using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Assume valid will allow to skip validation on blocks that are assumed to be valid.
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class AssumeValidRule : ConsensusRule
    {
        /// <inheritdoc />
        public override void Initialize()
        {
            // Currently assume valid depends that checkpoints exists and will execute first.
            this.Parent.Rules.FindRule<CheckpointsRule>();
        }

        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            // Check whether to use assumevalid switch to skip validation.
            if (!context.SkipValidation && (this.Parent.ConsensusSettings.BlockAssumedValid != null))
            {
                ChainedHeader assumeValidBlock = this.Parent.Chain.GetBlock(this.Parent.ConsensusSettings.BlockAssumedValid);
                context.SkipValidation = (assumeValidBlock != null) && (context.ValidationContext.ChainedHeader.Height <= assumeValidBlock.Height);
                if (context.SkipValidation)
                    this.Logger.LogTrace("Block validation will be partially skipped due to block height {0} is not greater than assumed valid block height {1}.", context.ValidationContext.ChainedHeader.Height, assumeValidBlock.Height);
            }

            return Task.CompletedTask;
        }
    }
}