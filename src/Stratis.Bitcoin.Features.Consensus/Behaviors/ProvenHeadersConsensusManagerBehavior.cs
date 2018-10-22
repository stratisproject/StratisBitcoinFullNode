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

        public ProvenHeadersConsensusManagerBehavior(
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IConsensusManager consensusManager,
            IPeerBanning peerBanning,
            ILoggerFactory loggerFactory,
            Network network,
            IChainState chainState) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.network = network;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.chainState = chainState;
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
        /// <summary>Constructs the proven headers payload from locator to consensus tip.</summary>
        /// <param name="locator">Block locator.</param>
        /// <param name="hashStop">Hash of the block after which constructing headers payload should stop.</param>
        /// <param name="lastHeader"><see cref="T:NBitcoin.ProvenBlockHeader" /> of the last header that was added to the <see cref="T:Stratis.Bitcoin.P2P.Protocol.Payloads.ProvenHeadersPayload" />.</param>
        /// <returns><see cref="T:Stratis.Bitcoin.P2P.Protocol.Payloads.ProvenHeadersPayload" /> with headers from locator towards consensus tip or <c>null</c> in case locator was invalid.</returns>
        protected override Payload ConstructHeadersPayload(BlockLocator locator, uint256 hashStop, out ChainedHeader lastHeader)
        {
            ChainedHeader fork = this.chain.FindFork(locator);

            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var headers = new ProvenHeadersPayload();
            foreach (ChainedHeader header in this.chain.EnumerateToTip(fork).Skip(1))
            {
                var posBock = new PosBlock(header.Header);
                ProvenBlockHeader provenBlockHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(posBock);
                lastHeader = header;
                headers.Headers.Add(provenBlockHeader);

                if ((header.HashBlock == hashStop) || (headers.Headers.Count == MaxItemsPerHeadersMessage))
                    break;
            }

            return headers;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ProvenHeadersConsensusManagerBehavior(
                this.chain,
                this.initialBlockDownloadState,
                this.consensusManager,
                this.peerBanning,
                this.loggerFactory,
                this.network,
                this.chainState);
        }

        /// <summary>
        /// Determines whether the specified peer supports Proven Headers and PH has been activated.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <returns>
        ///   <c>true</c> if is peer is PH enabled; otherwise, <c>false</c>.
        /// </returns>
        private bool IsPeerPhEnabled(INetworkPeer peer)
        {
            return peer.Version >= NBitcoin.Protocol.ProtocolVersion.PROVEN_HEADER_VERSION;
        }

        /// <summary>
        /// Determines whether the specified peer is Whitelisted.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <returns>
        ///   <c>true</c> if the specified peer is Whitelisted; otherwise, <c>false</c>.
        /// </returns>
        private bool IsPeerWhitelisted(INetworkPeer peer)
        {
            return peer.Behavior<IConnectionManagerBehavior>()?.Whitelisted == true;
        }


        private bool IsProvenHeaderActivated()
        {
            if (this.network.Consensus.Options is PosConsensusOptions options)
            {
                long currentHeight = this.chainState.ConsensusTip.Height;
                return (options.ProvenHeadersActivationHeight > 0) && (currentHeight >= options.ProvenHeadersActivationHeight);
            }

            return false;
        }

        /// <summary>
        /// Builds the GetHeadersPayload.
        /// </summary>
        /// <returns>The GetHeadersPayload instance.
        /// If the peer can serve PH, GetProvenHeadersPayload is returned, otherwise if it's a legacy peer but it's whitelisted,
        /// GetHeadersPayload is returned.
        /// If the attached peer is a legacy peer and it's not whitelisted, returns null.
        /// </returns>
        protected override GetHeadersPayload BuildGetHeadersPayload()
        {
            INetworkPeer peer = this.AttachedPeer;

            if (this.IsProvenHeaderActivated())
            {
                if (this.IsPeerPhEnabled(peer))
                {
                    return new GetProvenHeadersPayload()
                    {
                        BlockLocator = (this.ExpectedPeerTip ?? this.consensusManager.Tip).GetLocator(),
                        HashStop = null
                    };
                }
                // If the peer doesn't supports PH but it's whitelisted, issue a standard GetHeadersPayload
                else if (IsPeerWhitelisted(peer) || this.IsProvenHeaderActivated())
                    return base.BuildGetHeadersPayload();
                // If the peer doesn't support PH and isn't whitelisted, return null (stop synch attempt with legacy StratisX nodes).
                else
                    return null;
            }
            else
            {
                // If proven header isn't activated, build a legacy header request
                return base.BuildGetHeadersPayload();
            }
        }

        /// <summary>
        /// Processes the legacy GetHeaders message.
        /// Only whitelisted legacy peers are allowed to handle this message.
        /// </summary>
        /// <param name="peer">The peer.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        protected Task ProcessLegacyHeadersAsync(INetworkPeer peer, List<BlockHeader> headers)
        {
            // Only legacy peers are allowed to handle this message, or ph enabled peers before PH activation.
            if (!this.IsPeerPhEnabled(peer) || !this.IsProvenHeaderActivated())
            {
                return base.ProcessHeadersAsync(peer, headers);
            }

            return Task.CompletedTask;
        }
    }
}
