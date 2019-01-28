using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Consensus.Behaviors
{
    /// <summary>
    /// Behavior that takes care of proven headers protocol. It also keeps the notion of peer's consensus tip.
    /// </summary>
    public class ProvenHeadersConsensusManagerBehavior : ConsensusManagerBehavior
    {
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IConsensusManager consensusManager;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly IChainState chainState;
        private readonly ICheckpoints checkpoints;
        private readonly IProvenBlockHeaderStore provenBlockHeaderStore;
        private readonly int lastCheckpointHeight;
        private readonly CheckpointInfo lastCheckpointInfo;
        private readonly ConnectionManagerSettings connectionManagerSettings;

        /// <summary>
        /// Specify if the node is a gateway or not.
        /// Gateway are used internally by Stratis to prevent network split during the transition from Headers to ProvenHeaders protocol.
        /// Gateways can only receive headers and blocks from whitelisted nodes.
        /// </summary>
        private readonly bool isGateway;

        public ProvenHeadersConsensusManagerBehavior(
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IConsensusManager consensusManager,
            IPeerBanning peerBanning,
            ILoggerFactory loggerFactory,
            Network network,
            IChainState chainState,
            ICheckpoints checkpoints,
            IProvenBlockHeaderStore provenBlockHeaderStore,
            ConnectionManagerSettings connectionManagerSettings) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.network = network;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.chainState = chainState;
            this.checkpoints = checkpoints;
            this.provenBlockHeaderStore = provenBlockHeaderStore;

            this.lastCheckpointHeight = this.checkpoints.GetLastCheckpointHeight();
            this.lastCheckpointInfo = this.checkpoints.GetCheckpoint(this.lastCheckpointHeight);

            this.connectionManagerSettings = connectionManagerSettings;

            this.isGateway = this.connectionManagerSettings.IsGateway;
        }

        /// <inheritdoc />
        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        protected override async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case ProvenHeadersPayload provenHeaders:
                    await this.ProcessHeadersAsync(peer, provenHeaders.Headers.Cast<BlockHeader>().ToList()).ConfigureAwait(false);
                    break;

                case GetProvenHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case HeadersPayload headers:
                    await this.ProcessLegacyHeadersAsync(peer, headers.Headers).ConfigureAwait(false);
                    break;

                default:
                    // Rely on base.OnMessageReceivedAsync only if the message hasn't be already processed.
                    await base.OnMessageReceivedAsync(peer, message).ConfigureAwait(false);
                    break;
            }
        }

        /// <inheritdoc />
        protected override bool CanConsumeCache()
        {
            int height = this.consensusManager.Tip.Height;

            if (height == this.lastCheckpointHeight)
                return true;

            if (height > this.lastCheckpointHeight)
            {
                // Try cashe consumption every N blocks advanced by consensus.
                // N should be between 0 and max reorg. 20% of max reorg is a good random value.
                // Higher the N the better performance boost we can get.
                uint tryCacheEveryBlocksCount = this.network.Consensus.MaxReorgLength / 5;

                // After last checkpoint.
                if ((this.BestReceivedTip == null) || (height % tryCacheEveryBlocksCount == 0) || (height >= this.BestReceivedTip.Height))
                    return true;
            }

            return false;
        }

        /// <inheritdoc />
        protected override Payload ConstructHeadersPayload(GetHeadersPayload getHeadersPayload, out ChainedHeader lastHeader)
        {
            // If getHeadersPayload isn't a GetProvenHeadersPayload, return base implementation result
            if (!(getHeadersPayload is GetProvenHeadersPayload))
            {
                var headersPayload = base.ConstructHeadersPayload(getHeadersPayload, out lastHeader) as HeadersPayload;

                for (int i = 0; i < headersPayload.Headers.Count; i++)
                {
                    if (headersPayload.Headers[i] is ProvenBlockHeader phHeader)
                    {
                        BlockHeader newHeader = this.chain.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                        newHeader.Bits = phHeader.Bits;
                        newHeader.Time = phHeader.Time;
                        newHeader.Nonce = phHeader.Nonce;
                        newHeader.Version = phHeader.Version;
                        newHeader.HashMerkleRoot = phHeader.HashMerkleRoot;
                        newHeader.HashPrevBlock = phHeader.HashPrevBlock;

                        headersPayload.Headers[i] = newHeader;
                    }
                }

                return headersPayload;
            }

            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocator);
            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var provenHeadersPayload = new ProvenHeadersPayload();

            ChainedHeader header = this.GetLastHeaderToSend(fork, getHeadersPayload.HashStop);
            this.logger.LogDebug("Last header that will be sent in headers payload is '{0}'.", header);

            for (int heightIndex = header.Height; heightIndex > fork.Height; heightIndex--)
            {
                if (!(header.Header is ProvenBlockHeader provenBlockHeader))
                {
                    provenBlockHeader = this.provenBlockHeaderStore.GetAsync(header.Height).GetAwaiter().GetResult();

                    if (provenBlockHeader == null)
                    {
                        // Proven header is not available yet for this header.
                        // This can happen in case headers were requested by the peer right after we advanced consensus tip
                        // So at this moment proven header is not created or not yet saved to headers store for the block connected.
                        this.logger.LogDebug("No PH available for header '{0}'.", header);
                        this.logger.LogTrace("(-)[NO_PH_AVAILABLE]");
                        break;
                    }
                    else if (provenBlockHeader.GetHash() != header.HashBlock)
                    {
                        // Proven header is in the store, but with a wrong hash.
                        // This can happen in case of reorgs, when the store has not yet been updated.
                        // Without this check, we may send headers that aren't consecutive because are built from different branches, and the other peer may ban us.
                        this.logger.LogDebug("Stored PH hash is wrong. Expected: {0}, Found: {1}", header.Header.GetHash(), provenBlockHeader.GetHash());
                        this.logger.LogTrace("(-)[WRONG STORED PH]");
                        break;
                    }
                }

                lastHeader = header;

                provenHeadersPayload.Headers.Add(provenBlockHeader);

                header = header.Previous;
            }

            provenHeadersPayload.Headers.Reverse();

            return provenHeadersPayload;
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            return new ProvenHeadersConsensusManagerBehavior(
                this.chain,
                this.initialBlockDownloadState,
                this.consensusManager,
                this.peerBanning,
                this.loggerFactory,
                this.network,
                this.chainState,
                this.checkpoints,
                this.provenBlockHeaderStore,
                this.connectionManagerSettings);
        }

        /// <summary>
        /// Determines whether the specified peer supports Proven Headers and PH has been activated.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <returns>
        ///   <c>true</c> if is peer is PH enabled; otherwise, <c>false</c>.
        /// </returns>
        private bool CanPeerProcessProvenHeaders(INetworkPeer peer)
        {
            return peer.Version >= NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION;
        }

        /// <summary>
        /// If the last checkpoint is bellow consensus tip we do not need proven headers.
        /// </summary>
        /// <returns> <c>true</c> if  we need to validate proven headers.</returns>
        private int GetCurrentHeight()
        {
            int currentHeight = (this.BestReceivedTip ?? this.consensusManager.Tip).Height;

            return currentHeight;
        }

        /// <summary>
        /// Builds the <see cref="GetHeadersPayload"/>.
        /// </summary>
        /// <returns>The <see cref="GetHeadersPayload"/> instance.
        /// If the node is a gateway and the peer is not whitelisted, return null (gateways can sync only from whitelisted peers).
        /// If the peer can serve PH, <see cref="GetProvenHeadersPayload" /> is returned, otherwise if it's a legacy peer but it's whitelisted,
        /// <see cref="GetHeadersPayload"/> is returned.
        /// If the attached peer is a legacy peer and it's not whitelisted, returns null.
        /// </returns>
        protected override GetHeadersPayload BuildGetHeadersPayload()
        {
            INetworkPeer peer = this.AttachedPeer;

            bool aboveLastCheckpoint = this.GetCurrentHeight() >= this.lastCheckpointHeight;

            if (!aboveLastCheckpoint)
            {
                // If proven header isn't activated, build a legacy header request
                // TODO: If the current ExpectedPeerTip is less then MaxItemsPerHeadersMessage from the last checkpoint we can set the GetProvenHeadersPayload.HashStop to be the hash of the last checkpoint (this will prevent sending over regular headers beyond last checkpoint).
                return base.BuildGetHeadersPayload();
            }

            if (this.isGateway)
            {
                if (peer.IsWhitelisted())
                {
                    // A gateway node can only sync using regular headers and from whitelisted peers
                    this.logger.LogDebug("Node is a gateway, sync regular headers from whitelisted peer.");
                    this.logger.LogTrace("(-)[PEER_WHITELISTED_BY_GATEWAY]");
                    return base.BuildGetHeadersPayload();
                }

                this.logger.LogTrace("(-)[PEER_NOT_WHITELISTED_BY_GATEWAY]:null");
                return null;
            }

            if (this.CanPeerProcessProvenHeaders(peer))
            {
                return new GetProvenHeadersPayload()
                {
                    BlockLocator = (this.BestReceivedTip ?? this.consensusManager.Tip).GetLocator(),
                    HashStop = null
                };
            }

            // If the peer doesn't support PH and we are above last checkpoint
            // return null (stop synch).
            return null;
        }

        /// <inheritdoc />
        protected override Task ProcessHeadersAsync(INetworkPeer peer, List<BlockHeader> headers)
        {
            if (this.isGateway)
            {
                this.logger.LogDebug("Node is a gateway, cannot sync from Proven Headers. Ignoring received headers.");
                return Task.CompletedTask;
            }

            return base.ProcessHeadersAsync(peer, headers);
        }

        /// <summary>
        /// Processes the legacy GetHeaders message.
        /// Only whitelisted legacy peers are allowed to handle this message.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <param name="headers">The headers.</param>
        protected async Task ProcessLegacyHeadersAsync(INetworkPeer peer, List<BlockHeader> headers)
        {
            if (this.isGateway && peer.IsWhitelisted())
            {
                this.logger.LogDebug("Node is a gateway, can only sync regular headers from whitelisted peer.");
                await base.ProcessHeadersAsync(peer, headers);

                this.logger.LogTrace("(-)[GATEWAY_AND_WHITELISTED]");
                return;
            }

            bool belowLastCheckpoint = this.GetCurrentHeight() <= this.lastCheckpointHeight;
            if (!belowLastCheckpoint)
            {
                this.logger.LogTrace("(-)[ABOVE_LAST_CHECKPOINT]");
                return;
            }

            int distanceFromCheckPoint = this.lastCheckpointHeight - this.GetCurrentHeight();
            if (distanceFromCheckPoint < MaxItemsPerHeadersMessage)
            {
                bool checkpointFound = false;

                // Filter out headers that are above the last checkpoint hash
                for (int index = 0; index < headers.Count; index++)
                {
                    if (headers[index].GetHash() == this.lastCheckpointInfo.Hash)
                    {
                        if (index != headers.Count - 1)
                        {
                            headers.RemoveRange(index + 1, headers.Count - index - 1);
                        }

                        checkpointFound = true;
                        break;
                    }
                }

                if (!checkpointFound)
                {
                    // Checkpoint was not found in presented headers so we discard this batch
                    this.logger.LogTrace("(-)[CHECKPOINT_HEADER_NOT_FOUND]");
                    return;
                }
            }

            await base.ProcessHeadersAsync(peer, headers).ConfigureAwait(false);
        }
    }
}
