﻿using System;
using System.Linq;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Behaviour implementation that encapsulates <see cref="IPeerAddressManager"/>.
    /// <para>
    /// Subscribes to state change events from <see cref="NetworkPeer"/> and relays connection and handshake attempts to
    /// the <see cref="IPeerAddressManager"/> instance.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManagerBehaviour : NetworkPeerBehavior
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// See <see cref="PeerAddressManagerBehaviourMode"/> for the different modes and their
        /// explanations.
        /// </summary>
        public PeerAddressManagerBehaviourMode Mode { get; set; }

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// The amount of peers that can be discovered before
        /// <see cref="PeerDiscovery"/> stops finding new ones.
        /// </summary>
        public int PeersToDiscover { get; set; }

        public PeerAddressManagerBehaviour(IDateTimeProvider dateTimeProvider, IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.dateTimeProvider = dateTimeProvider;
            this.Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
            this.peerAddressManager = peerAddressManager;
            this.PeersToDiscover = 1000;
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.StateChanged += this.AttachedPeer_StateChanged;
            this.AttachedPeer.MessageReceived += this.AttachedPeer_MessageReceived;
        }

        private void AttachedPeer_MessageReceived(NetworkPeer peer, IncomingMessage message)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Advertise) != 0)
            {
                if (message.Message.Payload is GetAddrPayload getaddr)
                {
                    var peers = this.peerAddressManager.PeerSelector.SelectPeers().Take(1000).Select(p => p.NetworkAddress).ToArray();
                    peer.SendMessageVoidAsync(new AddrPayload(peers));
                }
            }

            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (message.Message.Payload is AddrPayload addr)
                    this.peerAddressManager.AddPeers(addr.Addresses, peer.RemoteSocketAddress);
            }
        }

        // TODO: We need to refactor this as the StateChanged event handlers only gets attached
        // AFTER the peer has connected, which means that we can never go:
        // if (peer.State == NetworkPeerState.Connected)
        // which is more intuitive.
        // This happens in PeerDiscovery as well where we connect and then disconnect straight after.
        private void AttachedPeer_StateChanged(NetworkPeer peer, NetworkPeerState previousState)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (peer.State <= NetworkPeerState.Disconnecting && previousState == NetworkPeerState.HandShaked)
                    this.peerAddressManager.PeerConnected(peer.PeerAddress.Endpoint, this.dateTimeProvider.GetUtcNow());

                if (peer.State == NetworkPeerState.HandShaked)
                    this.peerAddressManager.PeerHandshaked(peer.PeerAddress.Endpoint, this.dateTimeProvider.GetUtcNow());
            }
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.StateChanged -= this.AttachedPeer_StateChanged;
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

    /// <summary>
    /// Specifies how messages related to network peer discovery are handled.
    /// </summary>
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