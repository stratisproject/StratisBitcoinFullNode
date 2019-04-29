﻿using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The connector used to connect to peers added via peer discovery.
    /// </summary>
    public sealed class PeerConnectorDiscovery : PeerConnector
    {
        /// <summary>Maximum peer selection attempts.</summary>
        private const int MaximumPeerSelectionAttempts = 5;

        private readonly ILogger logger;

        public PeerConnectorDiscovery(
            IAsyncProvider asyncProvider,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            ISelfEndpointTracker selfEndpointTracker) :
            base(asyncProvider, dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, selfEndpointTracker)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Requirements.RequiredServices = NetworkPeerServices.Network;
        }

        /// <inheritdoc/>
        protected override void OnInitialize()
        {
            this.MaxOutboundConnections = this.ConnectionSettings.MaxOutboundConnections;
        }

        /// <summary>This connector is only started if there are NO peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return !this.ConnectionSettings.Connect.Any(); }
        }

        /// <inheritdoc/>
        [NoTrace]
        protected override void OnStartConnect()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        /// <inheritdoc/>
        public override async Task OnConnectAsync()
        {
            int peerSelectionFailed = 0;

            PeerAddress peer = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                if (peerSelectionFailed > MaximumPeerSelectionAttempts)
                {
                    peerSelectionFailed = 0;
                    peer = null;

                    this.logger.LogTrace("Selection failed, maximum amount of selection attempts reached.");
                    break;
                }

                peer = this.peerAddressManager.PeerSelector.SelectPeer();
                if (peer == null)
                {
                    this.logger.LogTrace("Selection failed, selector returned nothing.");
                    peerSelectionFailed++;
                    continue;
                }

                if (!peer.Endpoint.Address.IsValid())
                {
                    this.logger.LogTrace("Selection failed, peer endpoint is not valid '{0}'.", peer.Endpoint);
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -addnode collection don't
                // try and connect to it.
                bool peerExistsInAddNode = this.ConnectionSettings.AddNode.Any(p => p.MapToIpv6().Match(peer.Endpoint));
                if (peerExistsInAddNode)
                {
                    this.logger.LogTrace("Selection failed, peer exists in -addnode args '{0}'.", peer.Endpoint);
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -connect collection don't
                // try and connect to it.
                bool peerExistsInConnectNode = this.ConnectionSettings.Connect.Any(p => p.MapToIpv6().Match(peer.Endpoint));
                if (peerExistsInConnectNode)
                {
                    this.logger.LogTrace("Selection failed, peer exists in -connect args '{0}'.", peer.Endpoint);
                    peerSelectionFailed++;
                    continue;
                }

                break;
            }

            // If the peer selector returns nothing, we wait 2 seconds to
            // effectively override the connector's initial connection interval.
            if (peer == null)
            {
                this.logger.LogTrace("Selection failed, executing selection delay.");
                await Task.Delay(2000, this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
            }
            else
            {
                // Connect if local, ip range filtering disabled or ip range filtering enabled and peer in a different group.
                if (peer.Endpoint.Address.IsRoutable(false) && this.ConnectionSettings.IpRangeFiltering && this.PeerIsPartOfExistingGroup(peer))
                {
                    this.logger.LogTrace("(-)[RANGE_FILTERED]");
                    return;
                }

                this.logger.LogDebug("Attempting connection to {0}.", peer.Endpoint);

                await this.ConnectAsync(peer).ConfigureAwait(false);
            }
        }

        private bool PeerIsPartOfExistingGroup(PeerAddress peerAddress)
        {
            if (this.connectionManager.ConnectedPeers == null)
            {
                this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]:false");
                return false;
            }

            byte[] peerAddressGroup = peerAddress.Endpoint.MapToIpv6().Address.GetGroup();

            foreach (INetworkPeer endPoint in this.connectionManager.ConnectedPeers.ToList())
            {
                byte[] endPointGroup = endPoint.PeerEndPoint.MapToIpv6().Address.GetGroup();
                if (endPointGroup.SequenceEqual(peerAddressGroup))
                {
                    this.logger.LogTrace("(-)[SAME_GROUP]:true");
                    return true;
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }
    }
}