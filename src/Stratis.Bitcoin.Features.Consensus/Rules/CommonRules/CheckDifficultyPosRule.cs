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

            // TODO: In the future once we migrated to fully C# network it might be good to consider signaling in the block header the network type.

            // In order to calculate difficulty we need to know the if the block type is POW/POS.
            // This is only available when the block is downloaded (on the coinbase).
            // to validate headers before having the block itself the best we can do is
            // validate POS header after POW blocks era is finished.
            if (context.ValidationContext.ChainedHeader.Height + 2 > this.Parent.Network.Consensus.LastPOWBlock)
            {
                ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;
                BlockHeader first = chainedHeader.Previous.Header;
                BlockHeader second = chainedHeader.Previous.Previous.Header;

                Target nextWorkRequired = this.PosParent.StakeValidator.CalculateRetarget(first.Time, first.Bits, second.Time, context.Consensus.ProofOfStakeLimitV2);

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