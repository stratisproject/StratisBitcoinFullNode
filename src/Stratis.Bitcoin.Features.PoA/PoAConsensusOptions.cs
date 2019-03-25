﻿using System;
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

        /// <summary>Adds capability of voting for adding\kicking federation members and other things.</summary>
        public bool VotingEnabled { get; protected set; }

        /// <summary>Makes federation members kick idle members.</summary>
        /// <remarks>Requires voting to be enabled to be set <c>true</c>.</remarks>
        public bool AutoKickIdleMembers { get; protected set; }

        /// <summary>Time that federation member has to be idle to be kicked by others in case <see cref="AutoKickIdleMembers"/> is enabled.</summary>
        public uint FederationMemberMaxIdleTimeSeconds { get; protected set; }

        /// <summary>Initializes values for networks that use block size rules.</summary>
        public PoAConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost,
            List<PubKey> federationPublicKeys,
            uint targetSpacingSeconds,
            bool votingEnabled,
            bool autoKickIdleMembers,
            uint federationMemberMaxIdleTimeSeconds = 60 * 60 * 24 * 7)
                : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
            this.GenesisFederationPublicKeys = federationPublicKeys;
            this.TargetSpacingSeconds = targetSpacingSeconds;
            this.VotingEnabled = votingEnabled;
            this.AutoKickIdleMembers = autoKickIdleMembers;
            this.FederationMemberMaxIdleTimeSeconds = federationMemberMaxIdleTimeSeconds;

            if (this.AutoKickIdleMembers && !this.VotingEnabled)
                throw new ArgumentException("Voting should be enabled for automatic kicking to work.");
        }
    }
}
