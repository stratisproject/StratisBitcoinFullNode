using System;
using System.Collections.Generic;
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
    /// didn't produce a block in <see cref="PoAConsensusOptions.FederationMemberMaxIdleTimeMinutes"/>.
    /// </summary>
    public class IdleFederationMembersKicker : IDisposable
    {
        private readonly ISignals signals;

        private readonly Network network;

        private readonly IKeyValueRepository keyValueRepository;

        private readonly IConsensusManager consensusManager;

        private SubscriptionToken blockConnectedToken, fedMemberAddedToken, fedMemberKickedToken;

        /// <remarks>Active time is updated when member is added or produced a new block.</remarks>
        private Dictionary<PubKey, uint> fedMembersByLastActiveTime;

        private const string fedMembersByLastActiveTimeKey = "fedMembersByLastActiveTime";

        public IdleFederationMembersKicker(ISignals signals, Network network, IKeyValueRepository keyValueRepository, IConsensusManager consensusManager)
        {
            this.signals = signals;
            this.network = network;
            this.keyValueRepository = keyValueRepository;
            this.consensusManager = consensusManager;
        }

        public void Initialize(FederationManager federationManager)
        {
            this.blockConnectedToken = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.fedMemberAddedToken = this.signals.Subscribe<FedMemberAdded>(this.OnFedMemberAdded);
            this.fedMemberKickedToken = this.signals.Subscribe<FedMemberKicked>(this.OnFedMemberKicked);

            this.fedMembersByLastActiveTime = this.keyValueRepository.LoadValueJson<Dictionary<PubKey, uint>>(fedMembersByLastActiveTimeKey);

            if (this.fedMembersByLastActiveTime == null)
            {
                this.fedMembersByLastActiveTime = new Dictionary<PubKey, uint>();

                // Initialize federation members with genesis time.
                foreach (PubKey federationMember in federationManager.GetFederationMembers())
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
            this.fedMembersByLastActiveTime.Add(fedMemberAddedData.AddedMember, this.consensusManager.Tip.Header.Time);

            this.SaveMembersByLastActiveTime();
        }

        private void OnBlockConnected(BlockConnected obj)
        {
            // TODO Should check if kicking is needed.
            // kicking is needed when tip.Time - last active time > max allowed time


            // TODO tests

            throw new NotImplementedException();
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
