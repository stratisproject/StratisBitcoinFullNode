using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header.
    /// </summary>
    /// <exception cref="ConsensusErrors.HighHash">Thrown if block doesn't have a valid PoW header.</exception>
    public class CalculateStakeRule : ConsensusRule
    {
        /// <summary>Quick easy access to the POS rules.</summary>
        private PosConsensusRules posParent;

        public override void Initialize()
        {
            this.posParent = this.Parent as PosConsensusRules;

            Guard.NotNull(this.posParent, nameof(this.posParent));
        }

        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            context.SetStake();

            if (context.Stake.BlockStake.IsProofOfWork())
            {
                if (context.CheckPow && !context.BlockValidationContext.Block.Header.CheckProofOfWork(context.Consensus))
                {
                    this.Logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            context.NextWorkRequired = this.posParent.StakeValidator.GetNextTargetRequired(
                this.posParent.StakeChain, 
                context.BlockValidationContext.ChainedBlock.Previous, 
                context.Consensus, 
                context.Stake.BlockStake.IsProofOfStake());

            return Task.CompletedTask;
        }
    }
}