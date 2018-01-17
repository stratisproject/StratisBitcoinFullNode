﻿using System.Linq;
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
    /// The connector used to connect to peers added via peer discovery.
    /// </summary>
    public sealed class PeerConnectorDiscovery : PeerConnector
    {
        /// <summary>Maximum peer selection attempts.</summary>
        private const int MaximumPeerSelectionAttempts = 5;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Parameterless constructor for dependency injection.</summary>
        public PeerConnectorDiscovery(
            IAsyncLoopFactory asyncLoopFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager) :
            base(asyncLoopFactory, dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, connectionSettings, peerAddressManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Requirements.RequiredServices = NetworkPeerServices.Network;
        }

        /// <inheritdoc/>
        public override void OnInitialize()
        {
            // TODO: make sure that this is moved to a new config implementation when it's ready.
            this.MaxOutboundConnections = this.NodeSettings.ConfigReader.GetOrDefault("maxOutboundConnections", 8);
        }

        /// <summary>This connector is only started if there are NO peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return !this.ConnectionSettings.Connect.Any(); }
        }

        /// <inheritdoc/>
        public override void OnStartConnect()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        public override async Task OnConnectAsync()
        {
            this.logger.LogTrace("()");

            int peerSelectionFailed = 0;

            PeerAddress peer = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                if (peerSelectionFailed > MaximumPeerSelectionAttempts)
                {
                    peerSelectionFailed = 0;
                    peer = null;

                    this.logger.LogTrace("Peer selection failed, maximum amount of failed attempts reached.");
                    break;
                }

                peer = this.peerAddressManager.PeerSelector.SelectPeer();
                if (peer == null)
                {
                    this.logger.LogTrace("Peer selection failed, peer is null.");
                    peerSelectionFailed++;
                    continue;
                }

                if (!peer.EndPoint.Address.IsValid())
                {
                    this.logger.LogTrace("Peer selection failed, peer endpoint is not valid '{0}'.", peer.EndPoint);
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer is already connected just continue.
                if (this.IsPeerConnected(peer.EndPoint))
                {
                    this.logger.LogTrace("Peer selection failed, peer is already connected '{0}'.", peer.EndPoint);
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -addnode collection don't
                // try and connect to it.
                var peerExistsInAddNode = this.ConnectionSettings.AddNode.Any(p => p.MapToIpv6().Match(peer.EndPoint));
                if (peerExistsInAddNode)
                {
                    this.logger.LogTrace("Peer selection failed, peer exists in -addnode args '{0}'.", peer.EndPoint);
                    peerSelectionFailed++;
                    continue;
                }

                // If the peer exists in the -connect collection don't
                // try and connect to it.
                var peerExistsInConnectNode = this.ConnectionSettings.Connect.Any(p => p.MapToIpv6().Match(peer.EndPoint));
                if (peerExistsInConnectNode)
                {
                    this.logger.LogTrace("Peer selection failed, peer exists in -connect args '{0}'.", peer.EndPoint);
                    peerSelectionFailed++;
                    continue;
                }

                break;
            }

            this.logger.LogTrace("Peer selected: '{0}'", peer?.EndPoint);

            if (peer != null)
                await ConnectAsync(peer).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }
    }
}