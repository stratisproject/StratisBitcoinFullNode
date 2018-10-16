using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>Rules that might be thrown by consensus rules that are specific to PoA consensus.</summary>
    public static class PoAConsensusErrors
    {
        public static readonly ConsensusError InvalidHeaderBits = new ConsensusError("invalid-header-bits", "invalid header bits");
        public static readonly ConsensusError InvalidHeaderTimestamp = new ConsensusError("invalid-header-timestamp", "invalid header timestamp");
        public static readonly ConsensusError InvalidHeaderSignature = new ConsensusError("invalid-header-signature", "invalid header signature");
        public static readonly ConsensusError InvalidBlockSignature = new ConsensusError("invalid-block-signature", "invalid block signature");
    }
}
