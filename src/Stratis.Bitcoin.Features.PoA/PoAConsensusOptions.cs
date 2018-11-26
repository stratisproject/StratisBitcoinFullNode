using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusOptions : ConsensusOptions
    {
        /// <summary>Public keys of all federation members.</summary>
        /// <remarks>
        /// Blocks that are not signed with private keys that correspond
        /// to public keys from this list are considered to be invalid.
        /// </remarks>
        public List<PubKey> FederationPublicKeys { get; protected set; }

        public uint TargetSpacingSeconds { get; protected set; }

        /// <summary>Initializes values for networks that use block size rules.</summary>
        public PoAConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost,
            List<PubKey> federationPublicKeys,
            uint targetSpacingSeconds)
                : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
            this.FederationPublicKeys = federationPublicKeys;
            this.TargetSpacingSeconds = targetSpacingSeconds;
        }
    }
}
