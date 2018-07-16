using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

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
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            ISelfEndpointTracker selfEndpointTracker) :
            base(asyncLoopFactory, dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, selfEndpointTracker)
        {
            this.Requirements.RequiredServices = NetworkPeerServices.Nothing;
        }

        /// <inheritdoc/>
        public override void OnInitialize()
        {
            this.MaxOutboundConnections = this.ConnectionSettings.Connect.Count;

            // Add the endpoints from the -connect arg to the address manager
            foreach (IPEndPoint ipEndpoint in this.ConnectionSettings.Connect)
            {
                this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);
            }
        }

        /// <summary>This connector is only started if there are peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return this.ConnectionSettings.Connect.Any(); }
        }

        /// <inheritdoc/>
        public override void OnStartConnect()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;
        }

        /// <summary>
        /// Only connect to nodes as specified in the -connect node arg.
        /// </summary>
        public override async Task OnConnectAsync()
        {
            foreach (IPEndPoint ipEndpoint in this.ConnectionSettings.Connect)
            {
                if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    return;

                PeerAddress peerAddress = this.peerAddressManager.FindPeer(ipEndpoint);
                if (peerAddress != null && !this.IsPeerConnected(peerAddress.Endpoint))
                {
                    // IpRangeFiltering disabled for localhost nodes by default.
                    if (ipEndpoint.Address != null && !ipEndpoint.Address.IsLocal())
                    {
                        // Check if we are disabling IP range filtering:
                        // (Nodes disallow connection to peers in same range to prevent sybil attacks).
                        if (!this.ConnectionSettings.IpRangeFilteringDisabled)
                        {
                            // The already connected endpoints.
                            IPEndPoint[] connectedEndpoints = this.ConnectorPeers.Select(x => x.PeerEndPoint).ToArray();

                            // Check if new endpoint is part of the existing group collection.
                            System.Func<IPEndPoint, byte[]> getGroup = (a) => a.Address.GetGroup();
                            bool groupExist = connectedEndpoints.Any(a => getGroup(a).SequenceEqual(getGroup(peerAddress.Endpoint.MapToIpv6())));
                            if (groupExist)
                            {
                                continue;
                            }
                        }
                    }

                    await this.ConnectAsync(peerAddress).ConfigureAwait(false);
                }
            }
        }
    }
}