using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// A behaviour that will manage the lifetime of peers that aren't considered worthy to be served to DNS queries.
    /// </summary>
    public class UnreliablePeerBehavior : NetworkPeerBehavior
    {
        /// <summary>
        /// Defines the network the node runs on, e.g. regtest/testnet/mainnet.
        /// </summary>
        private readonly Network network;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Handle the lifetime of a peer.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>The node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>
        /// Available checkpoints
        /// </summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public UnreliablePeerBehavior(Network network, IChainState chainState, ILoggerFactory loggerFactory, IPeerBanning peerBanning, NodeSettings nodeSettings, ICheckpoints checkpoints)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(peerBanning, nameof(nodeSettings));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(checkpoints, nameof(checkpoints));

            this.network = network;
            this.chainState = chainState;
            this.loggerFactory = loggerFactory;
            this.peerBanning = peerBanning;
            this.nodeSettings = nodeSettings;
            this.checkpoints = checkpoints;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new UnreliablePeerBehavior(this.network, this.chainState, this.loggerFactory, this.peerBanning, this.nodeSettings, this.checkpoints);
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <summary>
        /// Event handler that is called when the node receives a network message from the attached peer.
        /// </summary>
        /// <param name="peer">Peer that sent us the message.</param>
        /// <param name="message">Received message.</param>
        /// <remarks>
        /// This handler only cares about "version" messages, which are only sent once per node.
        /// </remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case VersionPayload version:
                    // If current node is on POS, and ProvenHeaders is activated, check if current connected peer can serve Proven Headers.
                    // If it can't, disconnect from him and ban for few minutes
                    if (this.IsProvenHeaderActivated() && !this.CanServeProvenHeader(version))
                    {
                        this.logger.LogDebug("Peer '{0}' has been banned because can't serve proven headers. Peer Version: {1}", peer.RemoteSocketEndpoint, version.Version);
                        this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, "Can't serve proven headers.");
                    }
                    break;
            }

            return Task.CompletedTask;
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
            long currentHeight = this.chainState.ConsensusTip.Height;
            return currentHeight >= this.checkpoints.GetLastCheckpointHeight(); ;
        }

        /// <summary>
        /// Determines whether the specified peer can serve proven header.
        /// </summary>
        /// <param name="version">The version of the connected peer.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can serve proven header] the specified peer; otherwise, <c>false</c>.
        /// </returns>
        private bool CanServeProvenHeader(VersionPayload version)
        {
            return version.Version >= NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION;
        }
    }
}