using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
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

        public ProvenHeadersConsensusManagerBehavior(
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IConsensusManager consensusManager,
            IPeerBanning peerBanning,
            ILoggerFactory loggerFactory,
            Network network,
            IChainState chainState,
            ICheckpoints checkpoints,
            IProvenBlockHeaderStore provenBlockHeaderStore) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
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
        protected override Payload ConstructHeadersPayload(GetHeadersPayload getHeadersPayload, out ChainedHeader lastHeader)
        {
            // If getHeadersPayload isn't a GetProvenHeadersPayload, return base implementation result
            if (!(getHeadersPayload is GetProvenHeadersPayload))
            {
                return base.ConstructHeadersPayload(getHeadersPayload, out lastHeader);
            }

            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocator);
            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var headers = new ProvenHeadersPayload();
            foreach (ChainedHeader header in this.chain.EnumerateToTip(fork).Skip(1))
            {
                if (!(header.Header is ProvenBlockHeader provenBlockHeader))
                {
                    this.logger.LogTrace("Invalid proven header, try loading it from the store.");
                    provenBlockHeader = this.provenBlockHeaderStore.GetAsync(header.Height).GetAwaiter().GetResult();
                    if (provenBlockHeader == null)
                    {
                        this.logger.LogTrace("(-)[INVALID_PROVEN_HEADER]:{header}", header);
                        throw new ConsensusException("Proven header could not be found.");
                    }
                }

                lastHeader = header;
                headers.Headers.Add(provenBlockHeader);

                if ((header.HashBlock == getHeadersPayload.HashStop) || (headers.Headers.Count == MaxItemsPerHeadersMessage))
                    break;
            }

            return headers;
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
                this.provenBlockHeaderStore);
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
            var currentHeight = (this.ExpectedPeerTip ?? this.consensusManager.Tip).Height;

            return currentHeight;
        }

        /// <summary>
        /// Builds the <see cref="GetHeadersPayload"/>.
        /// </summary>
        /// <returns>The <see cref="GetHeadersPayload"/> instance.
        /// If the peer can serve PH, <see cref="GetProvenHeadersPayload" /> is returned, otherwise if it's a legacy peer but it's whitelisted,
        /// <see cref="GetHeadersPayload"/> is returned.
        /// If the attached peer is a legacy peer and it's not whitelisted, returns null.
        /// </returns>
        protected override GetHeadersPayload BuildGetHeadersPayload()
        {
            INetworkPeer peer = this.AttachedPeer;

            bool aboveLastCheckpoint = this.GetCurrentHeight() >= this.lastCheckpointHeight;
            if (aboveLastCheckpoint)
            {
                if (this.CanPeerProcessProvenHeaders(peer))
                {
                    return new GetProvenHeadersPayload()
                    {
                        BlockLocator = (this.ExpectedPeerTip ?? this.consensusManager.Tip).GetLocator(),
                        HashStop = null
                    };
                }
                else if (peer.IsWhitelisted())
                {
                    // If the peer doesn't supports PH but it's whitelisted, issue a standard GetHeadersPayload
                    return base.BuildGetHeadersPayload();
                }
                else
                {
                    // If the peer doesn't support PH and isn't whitelisted, return null (stop synch attempt with legacy StratisX nodes).
                    return null;
                }
            }
            else
            {
                // If proven header isn't activated, build a legacy header request
                // TODO: If the current ExpectedPeerTip is less then MaxItemsPerHeadersMessage from the last checkpoint we can set the GetProvenHeadersPayload.HashStop to be the hash of the last checkpoint (this will prevent sending over regular headers beyond last checkpoint).
                return base.BuildGetHeadersPayload();
            }
        }

        /// <summary>
        /// Processes the legacy GetHeaders message.
        /// Only whitelisted legacy peers are allowed to handle this message.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <param name="headers">The headers.</param>
        protected async Task ProcessLegacyHeadersAsync(INetworkPeer peer, List<BlockHeader> headers)
        {
            bool isLegacyWhitelistedPeer = (!this.CanPeerProcessProvenHeaders(peer) && peer.IsWhitelisted());

            // Allow legacy headers that came from whitelisted peer.
            if (isLegacyWhitelistedPeer)
            {
                await base.ProcessHeadersAsync(peer, headers).ConfigureAwait(false);
                return;
            }

            bool belowLastCheckpoint = this.GetCurrentHeight() <= this.lastCheckpointHeight;

            if (!belowLastCheckpoint)
                return;

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
