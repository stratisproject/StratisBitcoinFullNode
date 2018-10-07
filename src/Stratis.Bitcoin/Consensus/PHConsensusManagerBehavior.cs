using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class PHConsensusManagerBehavior : ConsensusManagerBehavior
    {
        private readonly ConcurrentChain chain;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IConsensusManager consensusManager;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

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
                case GetHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case HeadersPayload headers:
                    await this.ProcessHeadersAsync(peer, headers).ConfigureAwait(false);
                    break;

                case ProvenHeadersPayload provenHeaders:
                    await this.ProcessHeadersAsync(peer, provenHeaders).ConfigureAwait(false);
                    break;

                case GetProvenHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;
            }
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
        private async Task ProcessGetHeadersAsync(INetworkPeer peer, GetProvenHeadersPayload getProvenHeadersPayload)
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

            ProvenHeadersPayload headersPayload = this.ConstructHeadersPayload(getProvenHeadersPayload.BlockLocator, getProvenHeadersPayload.HashStop, out ProvenBlockHeader lastHeader);

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

        /// <summary>
        /// Processes "headers" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="headersPayload">Payload of "headers" message to process.</param>
        /// <remarks>
        /// "headers" message is sent in response to "getheaders" message or it is solicited
        /// by the peer when a new block is validated (unless in IBD).
        /// <para>
        /// When we receive "headers" message from the peer, we can adjust our knowledge
        /// of the peer's view of the chain. We update its pending tip, which represents
        /// the tip of the best chain we think the peer has.
        /// </para>
        /// </remarks>
        protected async Task ProcessHeadersAsync(INetworkPeer peer, ProvenHeadersPayload headersPayload)
        {
            var headers = new List<BlockHeader>(headersPayload.Headers);

            if (headers.Count == 0)
            {
                this.logger.LogTrace("Proven Headers payload with no headers was received. Assuming we're synced with the peer.");
                this.logger.LogTrace("(-)[NO_HEADERS]");
                return;
            }

            if (!this.ValidateHeadersPayload(peer, headersPayload, out string validationError))
            {
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, validationError);

                this.logger.LogTrace("(-)[VALIDATION_FAILED]");
                return;
            }

            using (await this.asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (this.cachedHeaders.Count > CacheSyncHeadersThreshold) // TODO when proven headers are implemented combine this with size threshold of N mb.
                {
                    // Ignore this message because cache is full.
                    this.logger.LogTrace("(-)[CACHE_IS_FULL]");
                    return;
                }

                // If queue is not empty, add to queue instead of calling CM.
                if (this.cachedHeaders.Count != 0)
                {
                    this.cachedHeaders.AddRange(headers);

                    this.logger.LogTrace("{0} headers were added to cache, new cache size is {1}.", headers.Count, this.cachedHeaders.Count);
                    this.logger.LogTrace("(-)[CACHED]");
                    return;
                }

                var consensusFactory = new PosConsensusFactory();

                ConnectNewHeadersResult result = await base.PresentHeadersLockedAsync(headers).ConfigureAwait(false);

                if (result == null)
                {
                    this.logger.LogTrace("Processing of {0} headers failed.", headers.Count);
                    this.logger.LogTrace("(-)[PROCESSING_FAILED]");
                    return;
                }

                this.ExpectedPeerTip = result.Consumed;
                this.UpdateBestSentHeader(this.ExpectedPeerTip);

                if (result.Consumed.HashBlock != headers.Last().GetHash())
                {
                    var posBock = new PosBlock(result.Consumed.Header);
                    ProvenBlockHeader provenBlockHeader = consensusFactory.CreateProvenBlockHeader(posBock);
                    int consumedCount = headers.IndexOf(provenBlockHeader) + 1;
                    this.cachedHeaders.AddRange(headers.Skip(consumedCount));

                    this.logger.LogTrace("{0} out of {1} items were not consumed and added to cache.", headers.Count - consumedCount, headers.Count);
                }

                if (this.cachedHeaders.Count == 0)
                    await this.ResyncAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Constructs the proven headers payload from locator to consensus tip.</summary>
        /// <param name="locator">Block locator.</param>
        /// <param name="hashStop">Hash of the block after which constructing headers payload should stop.</param>
        /// <param name="lastHeader"><see cref="ProvenBlockHeader"/> of the last header that was added to the <see cref="ProvenHeadersPayload"/>.</param>
        /// <returns><see cref="ProvenHeadersPayload"/> with headers from locator towards consensus tip or <c>null</c> in case locator was invalid.</returns>
        private ProvenHeadersPayload ConstructHeadersPayload(BlockLocator locator, uint256 hashStop, out ProvenBlockHeader lastHeader)
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

        /// <summary>Validates the proven headers payload.</summary>
        /// <param name="peer">The peer who sent the payload.</param>
        /// <param name="provenHeadersPayload">Proven Headers payload to validate.</param>
        /// <param name="validationError">The validation error that is set in case <c>false</c> is returned.</param>
        /// <returns><c>true</c> if payload was valid, <c>false</c> otherwise.</returns>
        private bool ValidateHeadersPayload(INetworkPeer peer, ProvenHeadersPayload provenHeadersPayload, out string validationError)
        {
            validationError = null;

            if (provenHeadersPayload.Headers.Count > MaxItemsPerHeadersMessage)
            {
                this.logger.LogDebug("Proven Headers payload with {0} headers was received. Protocol violation. Banning the peer.", provenHeadersPayload.Headers.Count);

                validationError = "Protocol violation.";

                this.logger.LogTrace("(-)[TOO_MANY_HEADERS]:false");
                return false;
            }

            // Check headers for consecutiveness.
            for (int i = 1; i < provenHeadersPayload.Headers.Count; i++)
            {
                if (provenHeadersPayload.Headers[i].HashPrevBlock != provenHeadersPayload.Headers[i - 1].GetHash())
                {
                    this.logger.LogDebug("Peer '{0}' presented non-consecutiveness hashes at position {1} with prev hash '{2}' not matching hash '{3}'.",
                        peer.RemoteSocketEndpoint, i, provenHeadersPayload.Headers[i].HashPrevBlock, provenHeadersPayload.Headers[i - 1].GetHash());

                    validationError = "Peer presented nonconsecutive headers.";

                    this.logger.LogTrace("(-)[NONCONSECUTIVE]:false");
                    return false;
                }
            }

            return true;
        }

        //protected override void AttachCore()
        //{
        //    // Initialize auto sync timer.
        //    int interval = (int)TimeSpan.FromMinutes(AutosyncIntervalMinutes).TotalMilliseconds;
        //    this.autosyncTimer = new Timer(async (o) =>
        //    {
        //        await this.ResyncAsync().ConfigureAwait(false);
        //    }, null, interval, interval);

        //    if (this.AttachedPeer.State == NetworkPeerState.Connected)
        //        this.AttachedPeer.MyVersion.StartHeight = this.consensusManager.Tip.Height;

        //    this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
        //    this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        //}

        /// <inheritdoc />
        public override object Clone()
        {
            return new PHConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory);
        }
    }
}
