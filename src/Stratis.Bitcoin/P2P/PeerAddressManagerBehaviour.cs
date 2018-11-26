using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
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

        /// <summary>
        /// Flag to make sure <see cref="GetAddrPayload"/> is only sent once.
        /// </summary>
        private bool sentAddress;

        public PeerAddressManagerBehaviour(IDateTimeProvider dateTimeProvider, IPeerAddressManager peerAddressManager, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
            this.Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
            this.peerAddressManager = peerAddressManager;
            this.PeersToDiscover = 1000;
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

                        if (this.sentAddress)
                        {
                            this.logger.LogTrace("Multiple GetAddr requests from peer. Not replying to avoid fingerprinting attack.");
                            return;
                        }

                        IPEndPoint[] endPoints = this.peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(1000).Select(p => p.Endpoint).ToArray();
                        var addressPayload = new AddrPayload(endPoints.Select(p => new NetworkAddress(p)).ToArray());
                        await peer.SendMessageAsync(addressPayload).ConfigureAwait(false);

                        this.logger.LogTrace("Sent address payload following GetAddr request.");

                        this.sentAddress = true;
                    }

                    if (message.Message.Payload is PingPayload ping || message.Message.Payload is PongPayload pong)
                    {
                        if (peer.State == NetworkPeerState.HandShaked)
                            this.peerAddressManager.PeerSeen(peer.PeerEndPoint, this.dateTimeProvider.GetUtcNow());
                    }
                }

                if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
                {
                    if (message.Message.Payload is AddrPayload addr)
                        this.peerAddressManager.AddPeers(addr.Addresses.Select(a => a.Endpoint).ToArray(), peer.RemoteSocketAddress);
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
            return new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager, this.loggerFactory)
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