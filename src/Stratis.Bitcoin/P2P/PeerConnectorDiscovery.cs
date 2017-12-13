using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers added via peer discovery.
    /// </summary>
    public sealed class PeerConnectorDiscovery : PeerConnector
    {
        /// <summary>Parameterless constructor for dependency injection.</summary>
        public PeerConnectorDiscovery(
            IAsyncLoopFactory asyncLoopFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager) :
            base(asyncLoopFactory, dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, peerAddressManager)
        {
        }

        /// <inheritdoc/>
        public override void OnInitialize()
        {
            this.GroupSelector = WellKnownPeerConnectorSelectors.ByNetwork;
            this.MaximumNodeConnections = 8;
            this.Requirements = new NetworkPeerRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Network
            };
        }

        /// <summary>This connector is only started if there are NO peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return !this.NodeSettings.ConnectionManager.Connect.Any(); }
        }

        /// <inheritdoc/>
        public override void OnStartConnectAsync()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        /// <inheritdoc/>
        public override PeerAddress FindPeerToConnectTo()
        {
            int peerSelectionFailed = 0;

            PeerAddress peer = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested && peerSelectionFailed < 50)
            {
                peer = this.peerAddressManager.Selector.SelectPeer();

                if (!peer.NetworkAddress.Endpoint.Address.IsValid())
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -addnode collection don't 
                // try and connect to it.
                var peerExistsInAddNode = this.NodeSettings.ConnectionManager.AddNode.Any(p => p.MapToIpv6().Match(peer.NetworkAddress.Endpoint));
                if (peerExistsInAddNode)
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -connect collection don't 
                // try and connect to it.
                var peerExistsInConnectNode = this.NodeSettings.ConnectionManager.Connect.Any(p => p.MapToIpv6().Match(peer.NetworkAddress.Endpoint));
                if (peerExistsInConnectNode)
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer is already connected just continue.
                if (this.IsPeerConnected(peer.NetworkAddress.Endpoint))
                {
                    peerSelectionFailed++;
                    continue;
                }

                break;
            }

            return peer;
        }
    }
}