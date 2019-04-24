using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>
    /// Provider of information about which pubkey should be used at which timestamp
    /// and what is the next timestamp at which current node will be able to mine.
    /// </summary>
    public interface ISlotsManager
    {
        /// <summary>Gets the federation member for specified timestamp.</summary>
        /// <param name="headerUnixTimestamp">Timestamp of a header.</param>
        /// <exception cref="ConsensusErrorException">In case timestamp is invalid.</exception>
        IFederationMember GetFederationMemberForTimestamp(uint headerUnixTimestamp, List<IFederationMember> federationMembers = null);

        /// <summary>Gets next timestamp at which current node can produce a block.</summary>
        /// <exception cref="Exception">Thrown if this node is not a federation member.</exception>
        uint GetMiningTimestamp(uint currentTime);

        /// <summary>Determines whether timestamp is valid according to the network rules.</summary>
        bool IsValidTimestamp(uint headerUnixTimestamp);

        uint GetRoundLengthSeconds(int federationMembersCount);
    }

    public class SlotsManager : ISlotsManager
    {
        private readonly PoAConsensusOptions consensusOptions;

        private readonly IFederationManager federationManager;

        private readonly ILogger logger;

        public SlotsManager(Network network, IFederationManager federationManager, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            this.federationManager = Guard.NotNull(federationManager, nameof(federationManager));

            this.consensusOptions = (network as PoANetwork).ConsensusOptions;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public IFederationMember GetFederationMemberForTimestamp(uint headerUnixTimestamp, List<IFederationMember> federationMembers = null)
        {
            if (!this.IsValidTimestamp(headerUnixTimestamp))
                PoAConsensusErrors.InvalidHeaderTimestamp.Throw();

            if (federationMembers == null)
                federationMembers = this.federationManager.GetFederationMembers();

            uint roundTime = this.GetRoundLengthSeconds(federationMembers.Count);

            // Time when current round started.
            uint roundStartTimestamp = (headerUnixTimestamp / roundTime) * roundTime;

            // Slot number in current round.
            int currentSlotNumber = (int)((headerUnixTimestamp - roundStartTimestamp) / this.consensusOptions.TargetSpacingSeconds);

            return federationMembers[currentSlotNumber];
        }

        /// <inheritdoc />
        public uint GetMiningTimestamp(uint currentTime)
        {
            if (!this.federationManager.IsFederationMember)
                throw new NotAFederationMemberException();

            List<IFederationMember> federationMembers = this.federationManager.GetFederationMembers();

            // Round length in seconds.
            uint roundTime = this.GetRoundLengthSeconds(federationMembers.Count);

            // Index of a slot that current node can take in each round.
            uint slotIndex = (uint)federationMembers.FindIndex(x => x.PubKey == this.federationManager.CurrentFederationKey.PubKey);

            // Time when current round started.
            uint roundStartTimestamp = (currentTime / roundTime) * roundTime;
            uint nextTimestampForMining = roundStartTimestamp + slotIndex * this.consensusOptions.TargetSpacingSeconds;

            // We already passed our slot in this round.
            // Get timestamp of our slot from next round.
            if (nextTimestampForMining < currentTime)
            {
                // Get timestamp for next round.
                nextTimestampForMining = roundStartTimestamp + roundTime + slotIndex * this.consensusOptions.TargetSpacingSeconds;
            }

            return nextTimestampForMining;
        }

        /// <inheritdoc />
        public bool IsValidTimestamp(uint headerUnixTimestamp)
        {
            return (headerUnixTimestamp % this.consensusOptions.TargetSpacingSeconds) == 0;
        }

        public uint GetRoundLengthSeconds(int federationMembersCount)
        {
            uint roundLength = (uint)(federationMembersCount * this.consensusOptions.TargetSpacingSeconds);

            return roundLength;
        }
    }
}
