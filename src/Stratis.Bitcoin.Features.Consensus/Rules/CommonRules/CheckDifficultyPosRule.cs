using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Calculate the difficulty for a POS network and check that it is correct.
    /// This rule is only activated after the POW epoch is finished according to the value in <see cref="Consensus.LastPOWBlock"/>.
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class CheckDifficultyPosRule : StakeStoreConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadDiffBits">Thrown if proof of stake is incorrect.</exception>
        public override void Run(RuleContext context)
        {
            if (context.Consensus.PowNoRetargeting)
            {
                return;
            }

            if (this.Parent.Network.Consensus.LastPOWBlock + 2 > context.ValidationContext.ChainedHeader.Height)
            {
                ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;
                Target nextWorkRequired = this.PosParent.StakeValidator.GetNextTargetRequired(chainedHeader, chainedHeader.Previous, chainedHeader.Previous.Previous, context.Consensus.ProofOfStakeLimitV2);

                BlockHeader header = context.ValidationContext.Block.Header;

                // Check proof of stake.
                if (header.Bits != nextWorkRequired)
                {
                    this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                    ConsensusErrors.BadDiffBits.Throw();
                }
            }
        }
    }
}