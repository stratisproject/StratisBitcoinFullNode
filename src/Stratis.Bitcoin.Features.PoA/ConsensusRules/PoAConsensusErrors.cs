using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    public static class PoAConsensusErrors
    {
        public static readonly ConsensusError InvalidHeaderBits = new ConsensusError("invalid-header-bits", "invalid header bits");
        public static readonly ConsensusError InvalidHeaderTimestamp = new ConsensusError("invalid-header-timestamp", "invalid header timestamp");
    }
}
