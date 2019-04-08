using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Behaviour implementation that encapsulates <see cref="IPeerAddressManager"/>.
    /// <para>
    /// Subscribes to state change events from <see cref="INetworkPeer"/> and relays connection and handshake attempts to
    /// the <see cref="IPeerAddressManager"/> instance.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManagerBehaviour : NetworkPeerBehavior
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Builds loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>See <see cref="PeerAddressManagerBehaviourMode"/> for the different modes and their explanations.</summary>
        public PeerAddressManagerBehaviourMode Mode { get; set; }

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        private readonly IPeerBanning peerBanning;

        /// <summary>The maximum amount of addresses per addr payload. </summary>
        /// <remarks><see cref="https://en.bitcoin.it/wiki/Protocol_documentation#addr"/>.</remarks>
        private const int MaxAddressesPerAddrPayload = 1000;

        /// <summary>Flag to make sure <see cref="GetAddrPayload"/> is only sent once.</summary>
        /// TODO how does it help against peer reconnecting to reset the flag?
        private bool addrPayloadSent;

        public PeerAddressManagerBehaviour(IDateTimeProvider dateTimeProvider, IPeerAddressManager peerAddressManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            Guard.NotNull(peerAddressManager, nameof(peerBanning));
            Guard.NotNull(peerAddressManager, nameof(loggerFactory));

            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
            this.peerBanning = peerBanning;
            this.Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
            this.peerAddressManager = peerAddressManager;
            this.addrPayloadSent = false;
        }

        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);

            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (this.AttachedPeer.State == NetworkPeerState.Connected)
                    this.peerAddressManager.PeerConnected(this.AttachedPeer.PeerEndPoint, this.dateTimeProvider.GetUtcNow());
            }
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                if ((this.Mode & PeerAddressManagerBehaviourMode.Advertise) != 0)
                {
                    if (message.Message.Payload is GetAddrPayload)
                    {
                        if (!peer.Inbound)
                        {
                            this.logger.LogTrace("Outbound peer sent {0}. Not replying to avoid fingerprinting attack.", nameof(GetAddrPayload));
                            return;
                        }

                        if (this.addrPayloadSent)
                        {
                            this.logger.LogTrace("Multiple GetAddr requests from peer. Not replying to avoid fingerprinting attack.");
                            return;
                        }

                        IEnumerable<IPEndPoint> endPoints = this.peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(MaxAddressesPerAddrPayload).Select(p => p.Endpoint);
                        var addressPayload = new AddrPayload(endPoints.Select(p => new NetworkAddress(p)).ToArray());

                        await peer.SendMessageAsync(addressPayload).ConfigureAwait(false);

                        this.logger.LogTrace("Sent address payload following GetAddr request.");

                        this.addrPayloadSent = true;
                    }

                    if ((message.Message.Payload is PingPayload) || (message.Message.Payload is PongPayload))
                    {
                        if (peer.State == NetworkPeerState.HandShaked)
                            this.peerAddressManager.PeerSeen(peer.PeerEndPoint, this.dateTimeProvider.GetUtcNow());
                    }
                }

                if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
                {
                    if (message.Message.Payload is AddrPayload addr)
                    {
                        if (addr.Addresses.Length > MaxAddressesPerAddrPayload)
                        {
                            // Not respecting the protocol.
                            this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, "Protocol violation: addr payload size is limited by 1000 entries.");

                            this.logger.LogTrace("(-)[PROTOCOL_VIOLATION]");
                            return;
                        }

                        this.peerAddressManager.AddPeers(addr.Addresses.Select(a => a.Endpoint), peer.RemoteSocketAddress);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState previousState)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (peer.State == NetworkPeerState.HandShaked)
                    this.peerAddressManager.PeerHandshaked(peer.PeerEndPoint, this.dateTimeProvider.GetUtcNow());
            }

            if ((peer.Inbound) && (peer.State == NetworkPeerState.HandShaked) &&
                (this.Mode == PeerAddressManagerBehaviourMode.Advertise || this.Mode == PeerAddressManagerBehaviourMode.AdvertiseDiscover))
            {
                this.logger.LogTrace("[INBOUND] {0}:{1}, {2}:{3}, {4}:{5}", nameof(peer.RemoteSocketAddress), peer.RemoteSocketAddress, nameof(peer.RemoteSocketEndpoint), peer.RemoteSocketEndpoint, nameof(peer.RemoteSocketPort), peer.RemoteSocketPort);
                this.logger.LogTrace("[INBOUND] {0}:{1}, {2}:{3}", nameof(peer.PeerVersion.AddressFrom), peer.PeerVersion?.AddressFrom, nameof(peer.PeerVersion.AddressReceiver), peer.PeerVersion?.AddressReceiver);
                this.logger.LogTrace("[INBOUND] {0}:{1}", nameof(peer.PeerEndPoint), peer.PeerEndPoint);

                IPEndPoint inboundPeerEndPoint = null;

                // Use AddressFrom if it is not a Loopback address as this means the inbound node was configured with a different external endpoint.
                if (!peer.PeerVersion.AddressFrom.Match(new IPEndPoint(IPAddress.Loopback, this.AttachedPeer.Network.DefaultPort)))
                {
                    inboundPeerEndPoint = peer.PeerVersion.AddressFrom;
                }
                else
                {
                    // If it is a Loopback address use PeerEndpoint but combine it with the AdressFrom's port as that is the
                    // other node's listening port.
                    inboundPeerEndPoint = new IPEndPoint(peer.PeerEndPoint.Address, peer.PeerVersion.AddressFrom.Port);
                }

                this.logger.LogTrace("{0}", inboundPeerEndPoint);

                this.peerAddressManager.AddPeer(inboundPeerEndPoint, IPAddress.Loopback);
            }

            return Task.CompletedTask;
        }

        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);
        }

        [NoTrace]
        public override object Clone()
        {
            return new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager, this.peerBanning, this.loggerFactory) { Mode = this.Mode };
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