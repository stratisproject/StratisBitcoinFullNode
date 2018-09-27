using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Check that the block signature for a POS block is in the canonical format.
    /// </summary>
    public class PosBlockSignatureRepresentationRule : IntegrityValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockSignature">The block signature is not in the canonical format.</exception>
        public override void Run(RuleContext context)
        {
            if (!PosBlockValidator.IsCanonicalBlockSignature((PosBlock)context.ValidationContext.BlockToValidate, true))
            {
                ConsensusErrors.BadBlockSignature.Throw();
            }
        }
    }
}