using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

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
            var posRuleContext = context as PosRuleContext;

            posRuleContext.BlockStake = new BlockStake(context.ValidationContext.Block);

            if (posRuleContext.BlockStake.IsProofOfWork())
            {
                if (!context.MinedBlock && !context.ValidationContext.Block.Header.CheckProofOfWork())
                {
                    this.Logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            context.NextWorkRequired = this.PosParent.StakeValidator.GetNextTargetRequired(this.PosParent.StakeChain, context.ValidationContext.ChainedHeader.Previous, context.Consensus, posRuleContext.BlockStake.IsProofOfStake());

            return Task.CompletedTask;
        }
    }
}