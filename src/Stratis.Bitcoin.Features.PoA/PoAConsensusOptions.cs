using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusOptions : ConsensusOptions
    {
        /// <summary>Public keys of all federation members at the start of the chain.</summary>
        /// <remarks>
        /// Do not use this list anywhere except for at the initialization of the chain.
        /// Actual collection of the federation members can be changed with time.
        /// Use <see cref="FederationManager.GetFederationMembers"/> as a source of
        /// up to date federation keys.
        /// </remarks>
        public List<PubKey> GenesisFederationPublicKeys { get; protected set; }

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
            this.GenesisFederationPublicKeys = federationPublicKeys;
            this.TargetSpacingSeconds = targetSpacingSeconds;
        }
    }
}
