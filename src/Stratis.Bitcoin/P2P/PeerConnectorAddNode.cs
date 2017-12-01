using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers specified with the -addnode argument
    /// </summary>
    public sealed class PeerConnectorAddNode : PeerConnector
    {
        /// <summary>Constructor used for unit testing.</summary>
        public PeerConnectorAddNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
            : base(nodeSettings, peerAddressManager)
        {
        }

        /// <summary>Constructor used by <see cref="Connection.ConnectionManager"/>.</summary>
        public PeerConnectorAddNode(PeerConnectorContext context)
            : base(context)
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
            this.GroupSelector = WellKnownPeerConnectorSelectors.ByEndpoint;
            this.MaximumNodeConnections = this.NodeSettings.ConnectionManager.AddNode.Count;

            this.Requirements = new NetworkPeerRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Nothing
            };

            foreach (var endPoint in this.NodeSettings.ConnectionManager.AddNode)
            {
                this.peerAddressManager.AddPeer(new NetworkAddress(endPoint.MapToIpv6()), IPAddress.Loopback);
            }
        }

        /// <inheritdoc/>
        internal override NetworkAddress FindPeerToConnectTo()
        {
            PeerAddress peer = null;

            foreach (var endPoint in this.NodeSettings.ConnectionManager.AddNode)
            {
                peer = this.peerAddressManager.FindPeer(endPoint.MapToIpv6());
                if (peer == null)
                    continue;

                // If the peer is already connected just continue.
                if (this.IsPeerConnected(endPoint))
                    continue;
            }

            return peer.NetworkAddress;
        }
    }
}