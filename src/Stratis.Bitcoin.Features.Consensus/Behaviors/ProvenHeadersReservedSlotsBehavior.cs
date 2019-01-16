using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Consensus.Behaviors
{
    public class ProvenHeadersReservedSlotsBehavior : NetworkPeerBehavior
    {
        private readonly IConnectionManager connectionManager;
        private readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// The minimum peers supporting Proven Headers that we require to be connected to us.
        /// </summary>
        private const int MinimumRequiredPeerSupportingPH = 3;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ProvenHeadersReservedSlotsBehavior(
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory)
        {
            this.connectionManager = connectionManager;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        protected async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case VersionPayload version:
                    await this.ProcessVersionAsync(peer, version).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Processes "version" message received from the peer.
        /// Ensures we leave some available connection slots to nodes that are enabled to serve Proven Headers, if the number of minimum PH enabled nodes connected isn't met.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="version">Payload of "version" message to process.</param>
        private Task ProcessVersionAsync(INetworkPeer peer, VersionPayload version)
        {
            PeerConnectorDiscovery connector = this.connectionManager.PeerConnectors.OfType<PeerConnectorDiscovery>().FirstOrDefault();
            // If PeerConnectorDiscovery is not found it means we are using -connect and thus we don't enforce the rule.
            if (connector != null)
            {
                // Connector.ConnectorPeers returns only handshaked peers, and passed peer is negotiating versions and
                // it's not yet included in the ConnectorPeers collection.
                // freeSlots returns the number of available slots, considering that passed peer is taking one slot.
                int freeSlots = connector.MaxOutboundConnections - connector.ConnectorPeers.Count - 1;

                // Get the number of PH-enabled peers we are already connected to.
                int phEnabledPeersConnected = connector.ConnectorPeers.Count(p => this.DoesPeerSupportsPH(p.PeerVersion));

                if (freeSlots >= (MinimumRequiredPeerSupportingPH - phEnabledPeersConnected))
                {
                    // There are enough free slot to allow the minimum required ph-enabled peers to connect to us.
                    this.logger.LogTrace("Enough free slots. Free Slots: {0}, Required PH-enabled peers:{1}, Connected PH-enabled peers:{2}", freeSlots, MinimumRequiredPeerSupportingPH, phEnabledPeersConnected);
                    return Task.CompletedTask;
                }

                if (this.DoesPeerSupportsPH(version))
                {
                    INetworkPeer nodeToDisconnect = this.GetConnectedLegacyPeersSortedByTip(connector.ConnectorPeers).FirstOrDefault();
                    if (nodeToDisconnect != null)
                    {
                        this.logger.LogDebug("Disconnecting legacy peer ({0}). Can't serve Proven Header.", nodeToDisconnect.PeerEndPoint);
                        nodeToDisconnect.Disconnect("Reserving connection slot for a Proven Header enabled peer.");
                    }
                }
                else
                {
                    this.logger.LogDebug("Current peer ({0}) doesn't serve Proven Header. Reserving last slot for Proven Header peers.", peer.PeerEndPoint);
                    peer.Disconnect("Reserving connection slot for a Proven Header enabled peer.");
                }
            }

            return Task.CompletedTask;
        }

        private bool DoesPeerSupportsPH(VersionPayload peerVersion)
        {
            return peerVersion.Version >= NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION;
        }

        private IEnumerable<INetworkPeer> GetConnectedLegacyPeersSortedByTip(NetworkPeerCollection connectedPeers)
        {
            return from peer in connectedPeers.ToList() // not sure if connectedPeers can change, so i use ToList to get a snapshot
                   let isLegacy = peer.PeerVersion.Version < NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION
                   let tip = peer.Behavior<ProvenHeadersConsensusManagerBehavior>()?.BestReceivedTip?.Height ?? 0
                   where isLegacy
                   orderby tip
                   select peer;
        }

        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            return new ProvenHeadersReservedSlotsBehavior(
                this.connectionManager,
                this.loggerFactory
               );
        }
    }
}
