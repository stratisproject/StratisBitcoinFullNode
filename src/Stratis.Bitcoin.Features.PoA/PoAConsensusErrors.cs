using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>Rules that might be thrown by consensus rules that are specific to PoA consensus.</summary>
    public static class PoAConsensusErrors
    {
        public static ConsensusError InvalidHeaderBits => new ConsensusError("invalid-header-bits", "invalid header bits");

        public static ConsensusError InvalidHeaderTimestamp => new ConsensusError("invalid-header-timestamp", "invalid header timestamp");

        public static ConsensusError InvalidHeaderSignature => new ConsensusError("invalid-header-signature", "invalid header signature");

        public static ConsensusError InvalidBlockSignature => new ConsensusError("invalid-block-signature", "invalid block signature");

        // Voting related errors.
        public static ConsensusError TooManyVotingOutputs => new ConsensusError("too-many-voting-outputs", "there could be only 1 voting output");

        public static ConsensusError VotingDataInvalidFormat => new ConsensusError("invalid-voting-data-format", "voting data format is invalid");
    }
}
