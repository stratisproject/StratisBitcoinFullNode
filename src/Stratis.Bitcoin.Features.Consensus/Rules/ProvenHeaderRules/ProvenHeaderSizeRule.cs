using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Rule to check if the serialized sizes of the proven header components, such as merkle proof (max 512 bytes),
    /// signature (max 80 bytes) and coinstake (max 1,000,000 bytes), do not exceed maximum possible size allocation.
    /// </summary>
    /// <seealso cref="ProvenHeaderRuleBase" />
    public class ProvenHeaderSizeRule : ProvenHeaderRuleBase
    {
        /// <inheritdoc />
        protected override void ProcessRule(PosRuleContext context, ChainedHeader chainedHeader, ProvenBlockHeader header)
        {
            if (header.MerkleProofSize > PosConsensusOptions.MaxMerkleProofSerializedSize)
            {
                this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_MERKLE_PROOF_SIZE]");
                ConsensusErrors.BadProvenHeaderMerkleProofSize.Throw();
            }

            if (header.CoinstakeSize > PosConsensusOptions.MaxCoinstakeSerializedSize)
            {
                this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_COINSTAKE_SIZE]");
                ConsensusErrors.BadProvenHeaderCoinstakeSize.Throw();
            }

            if (header.SignatureSize > PosConsensusOptions.MaxBlockSignatureSerializedSize)
            {
                this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_SIGNATURE_SIZE]");
                ConsensusErrors.BadProvenHeaderSignatureSize.Throw();
            }
        }
    }
}
