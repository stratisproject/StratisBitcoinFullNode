using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Check that the block signature for a POS block is in the canonical format.
    /// </summary>
    public class PosBlockSignatureRepresentationRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            if (!PosBlockValidator.IsCanonicalBlockSignature((PosBlock)context.ValidationContext.BlockToValidate, false))
            {
                // In stratisX CANONICAL_BLOCK_SIG_VERSION (version.h) is defined as 70000, the same as ProtocolVersion.ALT_PROTOCOL_VERSION.
                if (this.Parent.NodeSettings.ProtocolVersion >= ProtocolVersion.ALT_PROTOCOL_VERSION)
                    ConsensusErrors.BadBlockSignature.Throw(); // bad block signature encoding
            }

            if (!PosBlockValidator.IsCanonicalBlockSignature((PosBlock)context.ValidationContext.BlockToValidate, true))
            {
                // In stratisX CANONICAL_BLOCK_SIG_LOW_S_VERSION (version.h) is defined as 70000, the same as ProtocolVersion.ALT_PROTOCOL_VERSION.
                if (this.Parent.NodeSettings.ProtocolVersion >= ProtocolVersion.ALT_PROTOCOL_VERSION)
                    ConsensusErrors.BadBlockSignature.Throw(); // bad block signature encoding (low-s)

                if (!PosBlockValidator.EnsureLowS(((PosBlock)context.ValidationContext.BlockToValidate).BlockSignature))
                    ConsensusErrors.BadBlockSignature.Throw(); // EnsureLowS failed
            }

            return Task.CompletedTask;
        }
    }
}