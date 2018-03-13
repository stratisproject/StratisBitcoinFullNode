using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoS header and calculate the next block difficulty.
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class CalculateStakeRule : StakeStoreConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash"> Thrown if block doesn't have a valid PoW header.</exception>
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

            context.NextWorkRequired = this.PosParent.StakeValidator.GetNextTargetRequired(this.PosParent.StakeChain, context.BlockValidationContext.ChainedBlock.Previous, context.Consensus, 
                context.Stake.BlockStake.IsProofOfStake());

            return Task.CompletedTask;
        }
    }
}