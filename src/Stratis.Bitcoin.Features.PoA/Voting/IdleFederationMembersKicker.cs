using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    /// <summary>
    /// Automatically schedules addition of voting data that votes for kicking federation member that
    /// didn't produce a block in <see cref="PoAConsensusOptions.FederationMemberMaxIdleTimeSeconds"/>.
    /// </summary>
    public class IdleFederationMembersKicker : IDisposable
    {
        private readonly ISignals signals;

        private readonly IKeyValueRepository keyValueRepository;

        private readonly IConsensusManager consensusManager;

        private readonly Network network;

        private readonly FederationManager federationManager;

        private readonly SlotsManager slotsManager;

        private readonly VotingManager votingManager;

        private readonly ILogger logger;

        private readonly uint federationMemberMaxIdleTimeSeconds;

        private SubscriptionToken blockConnectedToken, fedMemberAddedToken, fedMemberKickedToken;

        /// <remarks>Active time is updated when member is added or produced a new block.</remarks>
        private Dictionary<PubKey, uint> fedMembersByLastActiveTime;

        private const string fedMembersByLastActiveTimeKey = "fedMembersByLastActiveTime";

        public IdleFederationMembersKicker(ISignals signals, Network network, IKeyValueRepository keyValueRepository, IConsensusManager consensusManager,
            FederationManager federationManager, SlotsManager slotsManager, VotingManager votingManager, ILoggerFactory loggerFactory)
        {
            this.signals = signals;
            this.network = network;
            this.keyValueRepository = keyValueRepository;
            this.consensusManager = consensusManager;
            this.federationManager = federationManager;
            this.slotsManager = slotsManager;
            this.votingManager = votingManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.federationMemberMaxIdleTimeSeconds = ((PoAConsensusOptions)network.Consensus.Options).FederationMemberMaxIdleTimeSeconds;
        }

        public void Initialize()
        {
            this.blockConnectedToken = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.fedMemberAddedToken = this.signals.Subscribe<FedMemberAdded>(this.OnFedMemberAdded);
            this.fedMemberKickedToken = this.signals.Subscribe<FedMemberKicked>(this.OnFedMemberKicked);

            this.fedMembersByLastActiveTime = this.keyValueRepository.LoadValueJson<Dictionary<PubKey, uint>>(fedMembersByLastActiveTimeKey);

            if (this.fedMembersByLastActiveTime == null)
            {
                this.logger.LogDebug("No saved data found. Initializing federation data with genesis timestamps.");

                this.fedMembersByLastActiveTime = new Dictionary<PubKey, uint>();

                // Initialize federation members with genesis time.
                foreach (PubKey federationMember in this.federationManager.GetFederationMembers())
                    this.fedMembersByLastActiveTime.Add(federationMember, this.network.GenesisTime);

                this.SaveMembersByLastActiveTime();
            }
        }

        private void OnFedMemberKicked(FedMemberKicked fedMemberKickedData)
        {
            this.fedMembersByLastActiveTime.Remove(fedMemberKickedData.KickedMember);

            this.SaveMembersByLastActiveTime();
        }

        private void OnFedMemberAdded(FedMemberAdded fedMemberAddedData)
        {
            if (!this.fedMembersByLastActiveTime.ContainsKey(fedMemberAddedData.AddedMember))
            {
                this.fedMembersByLastActiveTime.Add(fedMemberAddedData.AddedMember, this.consensusManager.Tip.Header.Time);

                this.SaveMembersByLastActiveTime();
            }
        }

        private void OnBlockConnected(BlockConnected blockConnectedData)
        {
            // Update last active time.
            uint timestamp = blockConnectedData.ConnectedBlock.ChainedHeader.Header.Time;
            PubKey key = this.slotsManager.GetPubKeyForTimestamp(timestamp);
            this.fedMembersByLastActiveTime.AddOrReplace(key, timestamp);

            this.SaveMembersByLastActiveTime();

            // Check if any fed member was idle for too long.
            ChainedHeader tip = this.consensusManager.Tip;

            foreach (KeyValuePair<PubKey, uint> fedMemberToActiveTime in this.fedMembersByLastActiveTime)
            {
                uint inactiveForSeconds = tip.Header.Time - fedMemberToActiveTime.Value;

                if (inactiveForSeconds > this.federationMemberMaxIdleTimeSeconds)
                {
                    this.logger.LogDebug("Federation member '{0}' was inactive for {1} seconds and will be scheduled to be kicked.", fedMemberToActiveTime.Key, inactiveForSeconds);

                    this.votingManager.ScheduleVote(new VotingData()
                    {
                        Key = VoteKey.KickFederationMember,
                        Data = fedMemberToActiveTime.Key.ToBytes()
                    });
                }
            }
        }

        private void SaveMembersByLastActiveTime()
        {
            this.keyValueRepository.SaveValueJson(fedMembersByLastActiveTimeKey, this.fedMembersByLastActiveTime);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.blockConnectedToken != null)
            {
                this.signals.Unsubscribe(this.blockConnectedToken);
                this.signals.Unsubscribe(this.fedMemberAddedToken);
                this.signals.Unsubscribe(this.fedMemberKickedToken);
            }
        }
    }
}
