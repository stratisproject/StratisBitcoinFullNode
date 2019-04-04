using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Features.PoA;

namespace Stratis.Features.FederatedPeg.Collateral
{
    public class FederatedPegPoAConsensusOptions : PoAConsensusOptions
    {
        public List<FederationMember> GenesisFederationMembers { get; private set; }

        public FederatedPegPoAConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost,
            List<FederationMember> genesisFederationMembers,
            uint targetSpacingSeconds,
            bool votingEnabled,
            bool autoKickIdleMembers,
            uint federationMemberMaxIdleTimeSeconds = 60 * 60 * 24 * 7)
            : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost,
                genesisFederationMembers.Select(x => x.PubKey).ToList(), targetSpacingSeconds, votingEnabled, autoKickIdleMembers, federationMemberMaxIdleTimeSeconds)
        {
            this.GenesisFederationMembers = genesisFederationMembers;
        }
    }
}
