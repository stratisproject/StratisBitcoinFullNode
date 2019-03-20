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
    }
}
