using System;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Signals;

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

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        public IdleFederationMembersKicker(ISignals signals, Network network)
        {
            this.signals = signals;
            this.network = network;
        }

        public void Initialize()
        {
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(this.OnBlockDisconnected);
        }

        private void OnBlockConnected(BlockConnected obj)
        {
            throw new NotImplementedException();
        }

        private void OnBlockDisconnected(BlockDisconnected obj)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.blockConnectedSubscription != null)
            {
                this.signals.Unsubscribe(this.blockConnectedSubscription);
                this.signals.Unsubscribe(this.blockDisconnectedSubscription);
            }
        }

        /// <summary>Data structure that contains information about federation member's mining slot.</summary>
        private class SlotInfo : IBitcoinSerializable
        {
            public SlotInfo(uint timestamp, PubKey fedMemberKey, bool used)
            {
                this.Timestamp = timestamp;
                this.FedMemberKey = fedMemberKey;
                this.Used = used;
            }

            /// <summary>Slot's timestamp.</summary>
            public uint Timestamp;

            /// <summary>Key of the federation member that owns this slot.</summary>
            public PubKey FedMemberKey;

            /// <summary><c>true</c> if the slot was taken; <c>false</c> otherwise.</summary>
            public bool Used;

            /// <inheritdoc />
            public void ReadWrite(BitcoinStream stream)
            {
                stream.ReadWrite(ref this.Timestamp);
                stream.ReadWrite(ref this.FedMemberKey);
                stream.ReadWrite(ref this.Used);
            }

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{nameof(this.Timestamp)}:{this.Timestamp}, {nameof(this.FedMemberKey)}:{this.FedMemberKey}, {nameof(this.Used)}:{this.Used}";
            }
        }
    }
}
