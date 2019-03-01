using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Calculate the difficulty for a POS block and check that it is correct.
    /// This rule is only activated after the POW epoch is finished according to the value in <see cref="Consensus.LastPOWBlock"/>.
    /// </summary>
    public class CheckDifficultyPosRule : HeaderValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadDiffBits">Thrown if proof of stake is incorrect.</exception>
        public override void Run(RuleContext context)
        {
            if (this.Parent.Network.Consensus.PosNoRetargeting)
            {
                this.Logger.LogTrace("(-)[POS_NO_RETARGETING]");
                return;
            }

            // TODO: In the future once we migrated to fully C# network it might be good to consider signaling in the block header the network type.

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            // In order to calculate difficulty we need to know the if the block type is POW/POS.
            // This is only available when the block is downloaded (on the coinbase).
            // to validate headers before having the block itself the best we can do is
            // validate POS header after POW blocks era is finished.

            // The check requires the last two blocks be of the same algo type,
            // thats why we wait for at least 2 blocks beyond the last pow block.

            // Both POW and POW blocks will be checked in the partial validation rule CheckDifficultykHybridRule
            // this rule will have the full block and can determine the algo type.
            if (chainedHeader.Height > this.Parent.Network.Consensus.LastPOWBlock + 2)
            {
                BlockHeader first = chainedHeader.Previous.Header;
                BlockHeader second = chainedHeader.Previous.Previous.Header;

                Target nextWorkRequired = this.PosParent.StakeValidator.CalculateRetarget(first.Time, first.Bits, second.Time, this.Parent.Network.Consensus.ProofOfStakeLimitV2);

                BlockHeader header = context.ValidationContext.ChainedHeaderToValidate.Header;

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