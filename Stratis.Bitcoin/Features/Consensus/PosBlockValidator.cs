using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosBlockValidator
    {
        public static bool IsCanonicalBlockSignature(Block block, bool checkLowS)
        {
            if (BlockStake.IsProofOfWork(block))
                return block.BlockSignatur.IsEmpty();

            return checkLowS ?
                ScriptEvaluationContext.IsLowDerSignature(block.BlockSignatur.Signature) :
                ScriptEvaluationContext.IsValidSignatureEncoding(block.BlockSignatur.Signature);
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
