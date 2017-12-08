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
        /// <summary>Parameterless constructor for dependency injection.</summary>
        public PeerConnectorAddNode()
            : base()
        {
        }

        /// <summary>Constructor used for unit testing.</summary>
        public PeerConnectorAddNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
            : base(nodeSettings, peerAddressManager)
        {
        }

        /// <inheritdoc/>
        internal override void OnInitialize()
        {
            this.GroupSelector = WellKnownPeerConnectorSelectors.ByEndpoint;
            this.MaximumNodeConnections = this.NodeSettings.ConnectionManager.AddNode.Count;

            this.Requirements = new NetworkPeerRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Nothing
            };

            foreach (var ipEndpoint in this.NodeSettings.ConnectionManager.AddNode)
            {
                this.peerAddressManager.AddPeer(new NetworkAddress(ipEndpoint.MapToIpv6()), IPAddress.Loopback);
            }
        }

        /// <summary>This connector is always started </summary>
        internal override bool OnCanStartConnectAsync
        {
            get { return true; }
        }

        /// <inheritdoc/>
        internal override void OnStartConnectAsync()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        /// <inheritdoc/>
        public override NetworkAddress FindPeerToConnectTo()
        {
            foreach (var endPoint in this.NodeSettings.ConnectionManager.AddNode)
            {
                PeerAddress peerAddress = this.peerAddressManager.FindPeer(endPoint);
                if (peerAddress != null && !this.IsPeerConnected(peerAddress.NetworkAddress.Endpoint))
                    return peerAddress.NetworkAddress;
            }

            return null;
        }
    }
}