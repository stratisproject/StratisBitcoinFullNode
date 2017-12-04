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
        public PeerConnectorAddNode(
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