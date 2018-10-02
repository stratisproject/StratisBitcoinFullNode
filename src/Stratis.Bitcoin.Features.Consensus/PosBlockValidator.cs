using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosBlockValidator
    {
        public static bool IsCanonicalBlockSignature(PosBlock block, bool checkLowS)
        {
            if (BlockStake.IsProofOfWork(block))
                return block.BlockSignature.IsEmpty();

            // For POS blocks that have a signature we do not append a SIGHASH type at the end of the signature.
            // Therefore IsValidSignatureEncoding should be called with haveSigHash = false.

            return checkLowS ?
                ScriptEvaluationContext.IsLowDerSignature(block.BlockSignature.Signature, false) :
                ScriptEvaluationContext.IsValidSignatureEncoding(block.BlockSignature.Signature, false);
        }
    }
}
