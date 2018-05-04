using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosBlockValidator
    {
        public static bool IsCanonicalBlockSignature(PosBlock block, bool checkLowS)
        {
            if (BlockStake.IsProofOfWork(block))
                return block.BlockSignature.IsEmpty();

            return checkLowS ?
                ScriptEvaluationContext.IsLowDerSignature(block.BlockSignature.Signature) :
                ScriptEvaluationContext.IsValidSignatureEncoding(block.BlockSignature.Signature);
        }

        public static bool EnsureLowS(BlockSignature blockSignature)
        {
            var signature = new ECDSASignature(blockSignature.Signature);
            if (!signature.IsLowS)
                blockSignature.Signature = signature.MakeCanonical().ToDER();
            return true;
        }
    }
}
