using System.Linq;
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
        /// <summary>Constructor for dependency injection.</summary>
        public PeerConnectorConnectNode(
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager)
            :
            base(asyncLoopFactory, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, peerAddressManager)
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

        /// <summary>This connector is only started if there are peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return this.NodeSettings.ConnectionManager.Connect.Any(); }
        }

        /// <inheritdoc/>
        public override void OnStartConnectAsync()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;
        }

        /// <summary>
        /// Only return nodes as specified in the -connect node arg.
        /// </summary>
        public override PeerAddress FindPeerToConnectTo()
        {
            foreach (var ipEndpoint in this.NodeSettings.ConnectionManager.Connect)
            {
                PeerAddress peerAddress = this.peerAddressManager.FindPeer(ipEndpoint);
                if (peerAddress != null && !this.IsPeerConnected(peerAddress.NetworkAddress.Endpoint))
                    return peerAddress;
            }

            return null;
        }
    }
}