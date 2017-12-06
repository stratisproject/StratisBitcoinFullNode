using System.Net;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers specified with the -connect argument
    /// </summary>
    public sealed class PeerConnectorConnectNode : PeerConnector
    {
        /// <summary>Constructor used for unit testing.</summary>
        public PeerConnectorConnectNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
            : base(nodeSettings, peerAddressManager)
        {
        }

        /// <summary>Constructor used by <see cref="Connection.ConnectionManager"/>.</summary>
        public PeerConnectorConnectNode(
            IAsyncLoopFactory asyncLoopFactory,
            ILogger logger,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifeTime,
            NodeSettings nodeSettings,
            NetworkPeerConnectionParameters parameters,
            IPeerAddressManager peerAddressManager)
            :
            base(asyncLoopFactory, logger, network, networkPeerFactory, nodeLifeTime, nodeSettings, parameters, peerAddressManager)
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;
            this.GroupSelector = WellKnownPeerConnectorSelectors.ByEndpoint;
            this.MaximumNodeConnections = nodeSettings.ConnectionManager.Connect.Count;
            this.Requirements = new NetworkPeerRequirement
            {
                MinVersion = nodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Nothing
            };

            // Add the end points from the -connect arg to the address manager
            foreach (var endPoint in this.NodeSettings.ConnectionManager.Connect)
            {
                this.peerAddressManager.AddPeer(new NetworkAddress(endPoint.MapToIpv6()), IPAddress.Loopback);
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