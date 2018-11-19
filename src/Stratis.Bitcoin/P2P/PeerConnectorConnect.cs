﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
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
            // For the -connect connector, effectively disable burst mode by preventing high frequency connection attempts.
            // The initial -connect list will all have their connection attempts made in parallel regardless, so this
            // does not slow down the startup.
            this.burstConnectionInterval = TimeSpans.Second;

            this.MaxOutboundConnections = this.ConnectionSettings.Connect.Count;

            // Add the endpoints from the -connect arg to the address manager.
            foreach (IPEndPoint ipEndpoint in this.ConnectionSettings.Connect)
            {
                this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);
            }

            // Mark all peers as white listed
            this.CurrentParameters.TemplateBehaviors.OfType<ConnectionManagerBehavior>().Single().Whitelisted = true;
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
            await this.ConnectionSettings.Connect.ForEachAsync(this.ConnectionSettings.MaxOutboundConnections, this.nodeLifetime.ApplicationStopping,
                async (ipEndpoint, cancellation) =>
                {
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    PeerAddress peerAddress = this.peerAddressManager.FindPeer(ipEndpoint);
                    if (peerAddress != null && !this.IsPeerConnected(peerAddress.Endpoint))
                    {
                        await this.ConnectAsync(peerAddress).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
    }
}