using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Behaviors
{
    public class ProvenHeadersReservedSlotsBehavior : NetworkPeerBehavior
    {
        /// <summary>
        /// Defines the network the node runs on, e.g. regtest/testnet/mainnet.
        /// </summary>
        private readonly Network network;
        private readonly IConnectionManager connectionManager;
        private readonly ILoggerFactory loggerFactory;
        private readonly ConsensusSettings consensusSettings;
        private readonly ICheckpoints checkpoints;
        private readonly IChainState chainState;

        /// <summary>
        /// The proven header peers reserved slots threshold (%).
        /// Represents the percentage of maximum connectable peers that we reserve for peers that are able to serve proven headers.
        /// </summary>
        private const decimal ProvenHeaderPeersReservedSlotsThreshold = 0.4M;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ProvenHeadersReservedSlotsBehavior(
            Network network,
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            IChainState chainState)
        {
            this.network = network;
            this.connectionManager = connectionManager;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.consensusSettings = consensusSettings;
            this.checkpoints = checkpoints;
            this.chainState = chainState;
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
            bool isAssumedValidEnabled = this.consensusSettings.BlockAssumedValid != null;
            bool isAheadLastCheckpoint = this.consensusSettings.UseCheckpoints && (this.chainState.ConsensusTip.Height > this.checkpoints.GetLastCheckpointHeight());
            bool isAheadAssumedValid = isAssumedValidEnabled && (this.chainState.ConsensusTip.IsAssumedValid == false);

            // We reserve slots to Proven Header peers, when:
            // - PH is active
            // - we are ahead of last CheckPoint
            // - legacy peer connected exceeds the maximum allowed number (maxOutboundConnection - reservedSlots)
            if (this.IsProvenHeaderActivated() && isAheadLastCheckpoint && isAheadAssumedValid)
            {
                PeerConnectorDiscovery connector = this.connectionManager.PeerConnectors.OfType<PeerConnectorDiscovery>().FirstOrDefault();
                // If PeerConnectorDiscovery is not found means we are using other ways to connect peers (like -connect) and thus we don't enforce the rule.
                if (connector != null)
                {
                    int slotsReservedForProvenHeaderEnabledPeers = (int)Math.Round(connector.MaxOutboundConnections * ProvenHeaderPeersReservedSlotsThreshold, MidpointRounding.ToEven);
                    int maxLegacyPeersAllowed = connector.MaxOutboundConnections - slotsReservedForProvenHeaderEnabledPeers;
                    int legacyPeersConnectedCount = connector.ConnectorPeers
                        .Where(p => p.PeerVersion.Version < NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION)
                        .Count();

                    bool hasToReserveSlots = legacyPeersConnectedCount >= maxLegacyPeersAllowed;
                    if (hasToReserveSlots)
                    {
                        // If we were previously in IBD state, we should remove legacy peers to reserve slots for PH enabled peers.
                        int legacyPeersToDisconnect = legacyPeersConnectedCount - maxLegacyPeersAllowed;
                        if (legacyPeersToDisconnect > 0)
                        {
                            var peersToDisconnect = this.GetConnectedLegacyPeersSortedByTip(connector.ConnectorPeers).Take(legacyPeersToDisconnect);
                            foreach (var peerToDisconnect in peersToDisconnect)
                            {
                                peerToDisconnect.Disconnect("Reserving connection slot for a Proven Header enabled peer.");
                            }
                        }

                        // If current connected peer doesn't serve Proven Header, disconnect it.
                        if (version.Version < NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION)
                        {
                            peer.Disconnect("Reserving connection slot for a Proven Header enabled peer.");
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private IEnumerable<INetworkPeer> GetConnectedLegacyPeersSortedByTip(NetworkPeerCollection connectedPeers)
        {
            return from peer in connectedPeers.ToList() // not sure if connectedPeers can change, so i use ToList to get a snapshot
                   let isLegacy = peer.PeerVersion.Version < NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION
                   let tip = peer.Behavior<ProvenHeadersConsensusManagerBehavior>()?.ExpectedPeerTip?.Height ?? 0
                   where isLegacy
                   orderby tip
                   select peer;
        }

        /// <summary>
        /// Determines whether proven headers are activated based on the proven header activation height and applicable network.
        /// </summary>
        /// <returns>
        /// <c>true</c> if proven header height is past the activation height for the corresponding network;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool IsProvenHeaderActivated()
        {
            if (this.network.Consensus.Options is PosConsensusOptions options)
            {
                long currentHeight = this.chainState.ConsensusTip.Height;
                return (options.ProvenHeadersActivationHeight > 0) && (currentHeight >= options.ProvenHeadersActivationHeight);
            }

            return false;
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ProvenHeadersReservedSlotsBehavior(
                this.network,
                this.connectionManager,
                this.loggerFactory,
                this.consensusSettings,
                this.checkpoints,
                this.chainState
               );
        }
    }
}