using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Calculate the difficulty of a POS network for both Pow/POS blocks.
    /// </summary>
    [PartialValidationRule]
    public class CheckDifficultykHybridRule : StakeStoreConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash">Thrown if block doesn't have a valid PoW header.</exception>
        /// <exception cref="ConsensusErrors.BadDiffBits">Thrown if proof of stake is incorrect.</exception>
        public override Task RunAsync(RuleContext context)
        {
            var posRuleContext = context as PosRuleContext;

            posRuleContext.BlockStake = BlockStake.Load(context.ValidationContext.Block);

            if (posRuleContext.BlockStake.IsProofOfWork())
            {
                if (!context.MinedBlock && !context.ValidationContext.Block.Header.CheckProofOfWork())
                {
                    this.Logger.LogTrace("(-)[HIGH_HASH]");
                    ConsensusErrors.HighHash.Throw();
                }
            }

            Target nextWorkRequired = this.PosParent.StakeValidator.GetNextTargetRequired(this.PosParent.StakeChain, context.ValidationContext.ChainedHeader.Previous, context.Consensus, posRuleContext.BlockStake.IsProofOfStake());

            BlockHeader header = context.ValidationContext.Block.Header;

            // Check proof of stake.
            if (header.Bits != nextWorkRequired)
            {
                this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }

            return Task.CompletedTask;
        }
    }
}