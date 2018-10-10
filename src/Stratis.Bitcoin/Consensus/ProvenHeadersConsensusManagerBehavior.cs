using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>Behavior that takes care of proven headers protocol. It also keeps the notion of peer's consensus tip.</summary>
    public class ProvenHeadersConsensusManagerBehavior : ConsensusManagerBehavior
    {
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IConsensusManager consensusManager;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;
        private readonly PosConsensusFactory consensusFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ProvenHeadersConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.consensusFactory = new PosConsensusFactory();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <inheritdoc />
        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        protected override async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            await base.OnMessageReceivedAsync(peer, message).ConfigureAwait(false);
            switch (message.Message.Payload)
            {
                case ProvenHeadersPayload provenHeaders:
                    await this.ProcessHeadersAsync(peer, provenHeaders.Headers.Cast<BlockHeader>().ToList()).ConfigureAwait(false);
                    break;

                case GetProvenHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
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
                ProvenBlockHeader provenBlockHeader = this.consensusFactory.CreateProvenBlockHeader(posBock);
                lastHeader = header;
                headers.Headers.Add(provenBlockHeader);

                if ((header.HashBlock == hashStop) || (headers.Headers.Count == MaxItemsPerHeadersMessage))
                    break;
            }

            return headers;
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ProvenHeadersConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory);
        }
    }
}
