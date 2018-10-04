using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class PHConsensusManagerBehavior : ConsensusManagerBehavior
    {
        private readonly ConcurrentChain chain;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IConsensusManager consensusManager;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Protects write access to the <see cref="BestSentHeader"/>.</summary>
        private readonly object bestSentHeaderLock;

        /// <summary>Gets the best header sent using <see cref="ProvenHeadersPayload"/>.</summary>
        /// <remarks>Write access should be protected by <see cref="bestSentHeaderLock"/>.</remarks>
        public ProvenBlockHeader BestSentHeader { get; private set; }

        /// <summary>Maximum number of headers in <see cref="HeadersPayload"/> according to Bitcoin protocol.</summary>
        /// <seealso cref="https://en.bitcoin.it/wiki/Protocol_documentation#getheaders"/>
        private const int MaxItemsPerHeadersMessage = 2000;

        public PHConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.bestSentHeaderLock = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <summary>
        /// Processes "getprovenheaders" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="getProvenHeadersPayload">Payload of "getprovenheaders" message to process.</param>
        /// <remarks>
        /// "getprovenheaders" message is sent by the peer in response to "headers" message until an empty array is received.
        /// <para>
        /// This payload notifies peers of our current best validated height, which is held by consensus tip.
        /// </para>
        /// <para>
        /// If the peer is behind/equal to our best height an empty array is sent back.
        /// </para>
        /// </remarks>
        private async Task ProcessGetProvenHeadersAsync(INetworkPeer peer, GetProvenHeadersPayload getProvenHeadersPayload)
        {
            if (getProvenHeadersPayload.BlockLocator.Blocks.Count > BlockLocator.MaxLocatorSize)
            {
                this.logger.LogTrace("Peer '{0}' sent getprovenheader with oversized locator, disconnecting.", peer.RemoteSocketEndpoint);

                peer.Disconnect("Peer sent getprovenheaders with oversized locator");

                this.logger.LogTrace("(-)[LOCATOR_TOO_LARGE]");
                return;
            }

            // Ignoring "getprovenheaders" from peers because node is in initial block download unless the peer is whitelisted.
            // We don't want to reveal our position in IBD which can be used by attacker. Also we don't won't to deliver peers any blocks
            // because that will slow down our own syncing process.
            if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.Behavior<IConnectionManagerBehavior>().Whitelisted)
            {
                this.logger.LogTrace("(-)[IGNORE_ON_IBD]");
                return;
            }

            ProvenHeadersPayload headersPayload = this.ConstructProvenHeadersPayload(getProvenHeadersPayload.BlockLocator, getProvenHeadersPayload.HashStop, out ProvenBlockHeader lastHeader);

            if (headersPayload != null)
            {
                this.logger.LogTrace("{0} headers were selected for sending, last one is '{1}'.", headersPayload.Headers.Count, headersPayload.Headers.LastOrDefault()?.GetHash());

                try
                {
                    this.BestSentHeader = lastHeader;

                    await peer.SendMessageAsync(headersPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Unable to send headers message to peer '{0}'.", peer.RemoteSocketEndpoint);
                }
            }
        }

        /// <summary>Constructs the proven headers payload from locator to consensus tip.</summary>
        /// <param name="locator">Block locator.</param>
        /// <param name="hashStop">Hash of the block after which constructing headers payload should stop.</param>
        /// <param name="lastHeader"><see cref="ProvenBlockHeader"/> of the last header that was added to the <see cref="ProvenHeadersPayload"/>.</param>
        /// <returns><see cref="ProvenHeadersPayload"/> with headers from locator towards consensus tip or <c>null</c> in case locator was invalid.</returns>
        private ProvenHeadersPayload ConstructProvenHeadersPayload(BlockLocator locator, uint256 hashStop, out ProvenBlockHeader lastHeader)
        {
            ChainedHeader fork = this.chain.FindFork(locator);

            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var headers = new ProvenHeadersPayload();
            var consensusFactory = new PosConsensusFactory();
            foreach (ChainedHeader header in this.chain.EnumerateToTip(fork).Skip(1))
            {
                var posBock = new PosBlock(header.Header);
                ProvenBlockHeader provenBlockHeader = consensusFactory.CreateProvenBlockHeader(posBock);
                lastHeader = provenBlockHeader;
                headers.Headers.Add(provenBlockHeader);

                if ((header.HashBlock == hashStop) || (headers.Headers.Count == MaxItemsPerHeadersMessage))
                    break;
            }

            return headers;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new PHConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory);
        }
    }
}
