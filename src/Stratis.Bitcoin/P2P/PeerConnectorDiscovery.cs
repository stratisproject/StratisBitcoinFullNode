using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers added via peer discovery.
    /// </summary>
    public sealed class PeerConnectorDiscovery : PeerConnector
    {
        /// <summary>Parameterless constructor for dependency injection.</summary>
        public PeerConnectorDiscovery()
            : base()
        {
        }

        /// <summary>Constructor used for unit testing.</summary>
        public PeerConnectorDiscovery(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
            : base(nodeSettings, peerAddressManager)
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

        /// <inheritdoc/>
        public override NetworkAddress FindPeerToConnectTo()
        {
            int peerSelectionFailed = 0;

            NetworkAddress peer = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested && peerSelectionFailed < 50)
            {
                peer = this.peerAddressManager.SelectPeerToConnectTo();

                if (!peer.Endpoint.Address.IsValid())
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -addnode collection don't 
                // try and connect to it.
                var peerExistsInAddNode = this.NodeSettings.ConnectionManager.AddNode.Any(p => p.MapToIpv6().Match(peer.Endpoint));
                if (peerExistsInAddNode)
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -connect collection don't 
                // try and connect to it.
                var peerExistsInConnectNode = this.NodeSettings.ConnectionManager.Connect.Any(p => p.MapToIpv6().Match(peer.Endpoint));
                if (peerExistsInConnectNode)
                {
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer is already connected just continue.
                if (this.IsPeerConnected(peer.Endpoint))
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