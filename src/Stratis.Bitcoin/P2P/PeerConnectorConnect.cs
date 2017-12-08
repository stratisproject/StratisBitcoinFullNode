using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers specified with the -connect argument
    /// </summary>
    public sealed class PeerConnectorConnectNode : PeerConnector
    {
        /// <summary>Parameterless constructor for dependency injection.</summary>
        public PeerConnectorConnectNode()
            : base()
        {
        }

        /// <summary>Constructor used for unit testing.</summary>
        public PeerConnectorConnectNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
            : base(nodeSettings, peerAddressManager)
        {
        }

        /// <inheritdoc/>
        public override void OnInitialize()
        {
            this.GroupSelector = WellKnownPeerConnectorSelectors.ByEndpoint;
            this.MaximumNodeConnections = this.NodeSettings.ConnectionManager.Connect.Count;
            this.Requirements = new NetworkPeerRequirement
            {
                MinVersion = this.NodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Nothing
            };

            // Add the endpoints from the -connect arg to the address manager
            foreach (var ipEndpoint in this.NodeSettings.ConnectionManager.Connect)
            {
                this.peerAddressManager.AddPeer(new NetworkAddress(ipEndpoint.MapToIpv6()), IPAddress.Loopback);
            }
        }

        /// <summary>
        /// Only return nodes as specified in the -connect node arg.
        /// </summary>
        public override PeerAddress FindPeerToConnectTo()
        {
            foreach (var endPoint in this.NodeSettings.ConnectionManager.Connect)
            {
                PeerAddress peerAddress = this.peerAddressManager.FindPeer(endPoint);
                if (peerAddress != null && !this.IsPeerConnected(peerAddress.NetworkAddress.Endpoint))
                    return peerAddress;
            }

            return null;
        }
    }
}