using System;
using System.Linq;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Behaviour implementation that encapsulates <see cref="IPeerAddressManager"/>.
    /// <para>
    /// Subscribes to state change events from <see cref="Node"/> and relays connection and handshake attempts to
    /// the <see cref="IPeerAddressManager"/> instance.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManagerBehaviour : NodeBehavior
    {
        public PeerAddressManagerBehaviour(IDateTimeProvider dateTimeProvider, IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.dateTimeProvider = dateTimeProvider;
            this.Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
            this.peerAddressManager = peerAddressManager;
            this.PeersToDiscover = 1000;
        }

        private readonly IDateTimeProvider dateTimeProvider;

        public int PeersToDiscover { get; set; }

        public PeerAddressManagerBehaviourMode Mode { get; set; }

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
        }

        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Advertise) != 0)
            {
                if (message.Message.Payload is GetAddrPayload getaddr)
                    node.SendMessageAsync(new AddrPayload(this.peerAddressManager.SelectPeersToConnectTo().Take(1000).ToArray()));
            }

            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (message.Message.Payload is AddrPayload addr)
                    this.peerAddressManager.AddPeers(addr.Addresses, node.RemoteSocketAddress, PeerIntroductionType.Discover);
            }
        }

        //TODO: We need to refactor this as the StateChanged event handlers only gets attached
        //AFTER the node has connected, which means that we can never go:
        //if (node.State == NodeState.Connected)
        //which is more intuitive.
        //This happens in PeerDiscovery as well where we connect and then disconnect straight after.
        private void AttachedNode_StateChanged(Node node, NodeState previousState)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (node.State <= NodeState.Disconnecting && previousState == NodeState.HandShaked)
                    this.peerAddressManager.PeerConnected(node.Peer.Endpoint, this.dateTimeProvider.GetUtcNow());

                if (node.State == NodeState.HandShaked)
                    this.peerAddressManager.PeerHandshaked(node.Peer.Endpoint, this.dateTimeProvider.GetUtcNow());
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
        }

        public override object Clone()
        {
            return new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager)
            {
                PeersToDiscover = this.PeersToDiscover,
                Mode = this.Mode
            };
        }
    }

    [Flags]
    public enum PeerAddressManagerBehaviourMode
    {
        /// <summary>Do not advertise nor discover new peers.</summary>
        None = 0,

        /// <summary>Only advertise known peers.</summary>
        Advertise = 1,

        /// <summary>Only discover peers.</summary>
        Discover = 2,

        /// <summary>Advertise known peer and discover peer.</summary>
        AdvertiseDiscover = 3,
    }
}
