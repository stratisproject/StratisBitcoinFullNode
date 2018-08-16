﻿using System;
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
    /// The connector used to connect to peers specified with the -addnode argument
    /// </summary>
    public sealed class PeerConnectorAddNode : PeerConnector
    {
        /// <summary>Constructor for dependency injection.</summary>
        public PeerConnectorAddNode(
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
            this.MaxOutboundConnections = this.ConnectionSettings.AddNode.Count;

            foreach (IPEndPoint ipEndpoint in this.ConnectionSettings.AddNode)
            {
                this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);
            }
        }

        /// <summary>This connector is always started.</summary>
        public override bool CanStartConnect
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public override void OnStartConnect()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        /// <summary>
        /// Only connect to nodes as specified in the -addnode arg.
        /// </summary>
        public override async Task OnConnectAsync()
        {
            await this.ConnectionSettings.AddNode.ForEachAsync(this.ConnectionSettings.MaxOutboundConnections, this.nodeLifetime.ApplicationStopping,
                async (ipEndpoint, cancellation) =>
                {
                    if (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        PeerAddress peerAddress = this.peerAddressManager.FindPeer(ipEndpoint);
                        if (peerAddress != null && !this.IsPeerConnected(peerAddress.Endpoint))
                        {
                            // Introduce a delay between attempts in case ConnectAsync fails instantly without the usual timeout.
                            if ((peerAddress.LastAttempt == null) || ((peerAddress.LastAttempt - DateTimeOffset.Now) >= this.defaultConnectionInterval))
                                await this.ConnectAsync(peerAddress).ConfigureAwait(false);
                            else
                                await Task.Delay(this.defaultConnectionInterval, this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                        }
                    }
                }).ConfigureAwait(false);
        }
    }
}
