using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosBlockValidator
    {
        public static bool IsCanonicalBlockSignature(PosBlock block, bool checkLowS)
        {
            if (BlockStake.IsProofOfWork(block))
                return block.BlockSignature.IsEmpty();

            // A signature should have only one representation, or malleability vectors are introduced.
            // Therefore, an ECDSA signature, per BIP66, must be in strict DER encoding.
            // Additionally, the 'S' value of the signature must be the lower of the two possible values.

            // Recall that, for an ECDSA signature, the R and S values are both modulo N (aka the curve order).
            // Further, a signature (R, S) is equivalent to (R, -S mod N).

            // In other words there are always 2 valid S values, call them S and S'.

            // A small example of why S + S' = N:
            // N = 7
            // S = 4
            // ((N - S) % N) = 3 = S', therefore S + S' = 7 = N

            // Given S + S' = N, there will always be one S value greater than half the curve order
            // (N / 2), and one less than this.
            // The canonical signature is required to use the so-called 'low S' value, the one less than N / 2.

            // Therefore to get the other S' value (the complement) we calculate S' = N - S.

            // We can switch between the canonical and non-canonical form by calculating the complement of
            // whichever representation we currently have in a signature.

            // Using N + S will give a valid signature too, but will not give the complement, as (N + S) mod N = S.

            // For POS blocks that have a signature we do not append a SIGHASH type at the end of the signature.
            // Therefore IsValidSignatureEncoding should be called with haveSigHash = false when validating
            // POS blocks.

            return checkLowS ?
                ScriptEvaluationContext.IsLowDerSignature(block.BlockSignature.Signature, false) :
                ScriptEvaluationContext.IsValidSignatureEncoding(block.BlockSignature.Signature, false);
        }
    }
}
