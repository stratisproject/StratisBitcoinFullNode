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
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>Behavior that takes care of headers protocol. It also keeps the notion of peer's consensus tip.</summary>
    public class ConsensusManagerBehavior : NetworkPeerBehavior
    {
        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <inheritdoc cref="ConsensusManager"/>
        private readonly IConsensusManager consensusManager;

        /// <inheritdoc cref="ConcurrentChain"/>
        protected ConcurrentChain chain;

        /// <inheritdoc cref="IPeerBanning"/>
        private readonly IPeerBanning peerBanning;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Our view of the peer's consensus tip constructed on peer's announcement of its tip using "headers" message.
        /// </summary>
        /// <remarks>
        /// The announced tip is accepted if it seems to be valid. Validation is only done on headers and so the announced tip may refer to invalid block.
        /// </remarks>
        public ChainedHeader BestReceivedTip { get; protected set; }

        /// <summary>Gets the best header sent using <see cref="HeadersPayload"/>.</summary>
        /// <remarks>Write access should be protected by <see cref="bestSentHeaderLock"/>.</remarks>
        public ChainedHeader BestSentHeader { get; protected set; }

        /// <summary>Timer that periodically tries to sync.</summary>
        private Timer autosyncTimer;

        /// <summary>Interval in minutes for the <see cref="autosyncTimer"/>.</summary>
        private const int AutosyncIntervalMinutes = 10;

        /// <summary>Amount of headers that should be cached until we stop syncing from the peer.</summary>
        protected const int CacheSyncHeadersThreshold = 2000;

        /// <summary>Maximum number of headers in <see cref="HeadersPayload"/> according to Bitcoin protocol.</summary>
        /// <seealso cref="https://en.bitcoin.it/wiki/Protocol_documentation#getheaders"/>
        protected const int MaxItemsPerHeadersMessage = 2000;

        /// <summary>List of block headers that were not yet consumed by <see cref="ConsensusManager"/>.</summary>
        /// <remarks>Should be protected by <see cref="asyncLock"/>.</remarks>
        protected readonly List<BlockHeader> cachedHeaders;

        /// <summary>Protects access to <see cref="cachedHeaders"/>.</summary>
        protected readonly AsyncLock asyncLock;

        /// <summary>Protects write access to the <see cref="BestSentHeader"/>.</summary>
        private readonly object bestSentHeaderLock;

        public ConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.chain = chain;
            this.peerBanning = peerBanning;

            this.cachedHeaders = new List<BlockHeader>();
            this.asyncLock = new AsyncLock();
            this.bestSentHeaderLock = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <summary>Presents cached headers to <see cref="ConsensusManager"/> from the cache if any and removes consumed from the cache.</summary>
        public async Task<ConnectNewHeadersResult> ConsensusTipChangedAsync()
        {
            ConnectNewHeadersResult result = null;
            bool syncRequired = false;

            using (await this.asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (this.cachedHeaders.Count != 0 && this.CanConsumeCache())
                {
                    result = await this.PresentHeadersLockedAsync(this.cachedHeaders, false).ConfigureAwait(false);

                    if ((result == null) || (result.Consumed == null))
                    {
                        if (result == null)
                            this.cachedHeaders.Clear();

                        this.logger.LogTrace("(-)[NO_HEADERS_CONNECTED]:null");
                        return null;
                    }

                    this.BestReceivedTip = result.Consumed;
                    this.UpdateBestSentHeader(this.BestReceivedTip);

                    int consumedCount = this.GetConsumedHeadersCount(this.cachedHeaders, result.Consumed.Header);

                    this.cachedHeaders.RemoveRange(0, consumedCount);
                    int cacheSize = this.cachedHeaders.Count;

                    this.logger.LogDebug("{0} entries were consumed from the cache, {1} items were left.", consumedCount, cacheSize);
                    syncRequired = cacheSize == 0;
                }
            }

            if (syncRequired)
                await this.ResyncAsync().ConfigureAwait(false);

            return result;
        }

        /// <summary>Determines whether cached headers (if any) should be pushed to consensus manager for consumption.</summary>
        protected virtual bool CanConsumeCache()
        {
            return true;
        }

        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        [NoTrace]
        protected virtual async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case HeadersPayload headers:
                    await this.ProcessHeadersAsync(peer, headers.Headers).ConfigureAwait(false);
                    break;

                case InvPayload inv:
                    await this.ProcessInvAsync(peer, inv.Inventory).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Processes "getheaders" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="getHeadersPayload">Payload of "getheaders" message to process.</param>
        /// <remarks>
        /// "getheaders" message is sent by the peer in response to "headers" message until an empty array is received.
        /// <para>
        /// This payload notifies peers of our current best validated height, which is held by consensus tip.
        /// </para>
        /// <para>
        /// If the peer is behind/equal to our best height an empty array is sent back.
        /// </para>
        /// </remarks>
        protected async Task ProcessGetHeadersAsync(INetworkPeer peer, GetHeadersPayload getHeadersPayload)
        {
            if (getHeadersPayload.BlockLocator.Blocks.Count > BlockLocator.MaxLocatorSize)
            {
                this.logger.LogDebug("Peer '{0}' sent getheader with oversized locator, disconnecting.", peer.RemoteSocketEndpoint);

                peer.Disconnect("Peer sent getheaders with oversized locator");

                this.logger.LogTrace("(-)[LOCATOR_TOO_LARGE]");
                return;
            }

            // Ignoring "getheaders" from peers because node is in initial block download unless the peer is whitelisted.
            // We don't want to reveal our position in IBD which can be used by attacker. Also we don't won't to deliver peers any blocks
            // because that will slow down our own syncing process.
            if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.IsWhitelisted())
            {
                this.logger.LogDebug("GetHeaders message from {0} was ignored because node is in IBD.", peer.PeerEndPoint);
                this.logger.LogTrace("(-)[IGNORE_ON_IBD]");
                return;
            }

            Payload headersPayload = this.ConstructHeadersPayload(getHeadersPayload, out ChainedHeader lastHeader);

            if (headersPayload != null)
            {
                try
                {
                    await peer.SendMessageAsync(headersPayload).ConfigureAwait(false);
                    this.BestSentHeader = lastHeader;
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebug("Unable to send headers message to peer '{0}'.", peer.RemoteSocketEndpoint);
                }
            }
        }

        /// <summary>Find last header that should be included in headers payload.</summary>
        protected ChainedHeader GetLastHeaderToSend(ChainedHeader fork, uint256 hashStop)
        {
            ChainedHeader lastHeader = this.consensusManager.Tip;

            // If the hash stop has been given, calculate the last chained header from it.
            if (hashStop != null && hashStop != uint256.Zero)
            {
                ChainedHeader hashStopHeader = lastHeader.FindAncestorOrSelf(hashStop);

                if ((hashStopHeader != null) && (lastHeader.Height > fork.Height))
                    lastHeader = hashStopHeader;
            }

            // Do not return more than 2000 headers from the fork point.
            if ((lastHeader.Height - fork.Height) > MaxItemsPerHeadersMessage)
            {
                // e.g. If fork = 3000 and tip is 6000 we need to start from block 5000.
                int startFromHeight = fork.Height + MaxItemsPerHeadersMessage;
                lastHeader = lastHeader.GetAncestor(startFromHeight);
            }

            return lastHeader;
        }

        /// <summary>Constructs the headers from locator to consensus tip.</summary>
        /// <param name="getHeadersPayload">The <see cref="GetHeadersPayload"/> payload that triggered the creation of this payload.</param>
        /// <param name="lastHeader"><see cref="ChainedHeader"/> of the last header that was added to the <see cref="HeadersPayload"/>.</param>
        /// <returns>Payload with headers from locator towards consensus tip or <c>null</c> in case locator was invalid.</returns>
        protected virtual Payload ConstructHeadersPayload(GetHeadersPayload getHeadersPayload, out ChainedHeader lastHeader)
        {
            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocator);

            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var headersPayload = new HeadersPayload();

            ChainedHeader header = this.GetLastHeaderToSend(fork, getHeadersPayload.HashStop);

            for (int heightIndex = header.Height; heightIndex > fork.Height; heightIndex--)
            {
                lastHeader = header;

                headersPayload.Headers.Add(header.Header);

                header = header.Previous;
            }

            this.logger.LogDebug("{0} headers were selected for sending, last one is '{1}'.", headersPayload.Headers.Count, headersPayload.Headers.LastOrDefault()?.GetHash());

            // We need to reverse it as it was added to the list backwards.
            headersPayload.Headers.Reverse();

            return headersPayload;
        }

        /// <summary>
        /// Processes "headers" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="headers">List of headers to process.</param>
        /// <remarks>
        /// "headers" message is sent in response to "getheaders" message or it is solicited
        /// by the peer when a new block is validated (unless in IBD).
        /// <para>
        /// When we receive "headers" message from the peer, we can adjust our knowledge
        /// of the peer's view of the chain. We update its pending tip, which represents
        /// the tip of the best chain we think the peer has.
        /// </para>
        /// </remarks>
        protected virtual async Task ProcessHeadersAsync(INetworkPeer peer, List<BlockHeader> headers)
        {
            this.logger.LogDebug("Received {0} headers. First: '{1}'  Last: '{2}'.", headers.Count, headers.FirstOrDefault()?.ToString(), headers.LastOrDefault()?.ToString());

            if (headers.Count == 0)
            {
                this.logger.LogDebug("Headers payload with no headers was received. Assuming we're synced with the peer.");
                this.logger.LogTrace("(-)[NO_HEADERS]");
                return;
            }

            if (!this.ValidateHeadersFromPayload(peer, headers, out string validationError))
            {
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, validationError);

                this.logger.LogDebug("Headers are invalid. Peer was banned.");
                this.logger.LogTrace("(-)[VALIDATION_FAILED]");
                return;
            }

            using (await this.asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (this.cachedHeaders.Count > CacheSyncHeadersThreshold) // TODO when proven headers are implemented combine this with size threshold of N mb.
                {
                    // Ignore this message because cache is full.
                    this.logger.LogDebug("Cache is full. Headers ignored.");
                    this.logger.LogTrace("(-)[CACHE_IS_FULL]");
                    return;
                }

                // If queue is not empty, add to queue instead of calling CM.
                if (this.cachedHeaders.Count != 0)
                {
                    uint256 cachedHeader = this.cachedHeaders.Last().GetHash();
                    uint256 prevNewHeader = headers.First().HashPrevBlock;

                    // Ensure headers can connect to cached headers.
                    if (cachedHeader == prevNewHeader)
                    {
                        this.cachedHeaders.AddRange(headers);

                        this.logger.LogDebug("{0} headers were added to cache, new cache size is {1}.", headers.Count, this.cachedHeaders.Count);
                        this.logger.LogTrace("(-)[HEADERS_ADDED_TO_CACHE]");
                        return;
                    }

                    // Workaround for special case when peer announces new block but we don't want to clean the cache.
                    // For example, we are busy syncing and have D E F in the cache (assume A B C are already in the
                    // CHT). The peer now advertises new block M because we haven't completed IBD yet.
                    // We do not want to clear the currently useful cache unnecessarily.
                    if (this.CheckIfUnsolicitedFutureHeader(headers))
                    {
                        this.logger.LogTrace("(-)[HEADER_FUTURE_CANT_CONNECT]");
                        return;
                    }

                    // The header(s) could not be connected and were not an unsolicited block advertisement.
                    this.cachedHeaders.Clear();
                    await this.ResyncAsync().ConfigureAwait(false);

                    this.logger.LogDebug("Header {0} could not be connected to last cached header {1}, clear cache and resync.", headers[0].GetHash(), cachedHeader);
                    this.logger.LogTrace("(-)[FAILED_TO_ATTACH_TO_CACHE]");
                    return;
                }

                ConnectNewHeadersResult result = await this.PresentHeadersLockedAsync(headers).ConfigureAwait(false);

                if (result == null)
                {
                    this.logger.LogDebug("Processing of {0} headers failed.", headers.Count);
                    this.logger.LogTrace("(-)[PROCESSING_FAILED]");

                    return;
                }

                if (result.Consumed == null)
                {
                    this.cachedHeaders.AddRange(headers);
                    this.logger.LogDebug("All {0} items were not consumed and added to cache.", headers.Count);

                    return;
                }

                this.BestReceivedTip = result.Consumed;
                this.UpdateBestSentHeader(this.BestReceivedTip);

                if (result.Consumed.HashBlock != headers.Last().GetHash())
                {
                    // Some headers were not consumed, add to cache.
                    int consumedCount = this.GetConsumedHeadersCount(headers, result.Consumed.Header);
                    this.cachedHeaders.AddRange(headers.Skip(consumedCount));

                    this.logger.LogDebug("{0} out of {1} items were not consumed and added to cache.", headers.Count - consumedCount, headers.Count);
                }

                if (this.cachedHeaders.Count == 0)
                    await this.ResyncAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Validates the headers payload.</summary>
        /// <param name="peer">The peer who sent the payload.</param>
        /// <param name="headers">Headers to validate.</param>
        /// <param name="validationError">The validation error that is set in case <c>false</c> is returned.</param>
        /// <returns><c>true</c> if payload was valid, <c>false</c> otherwise.</returns>
        protected bool ValidateHeadersFromPayload(INetworkPeer peer, List<BlockHeader> headers, out string validationError)
        {
            validationError = null;

            if (headers.Count > MaxItemsPerHeadersMessage)
            {
                this.logger.LogDebug("Headers payload with {0} headers was received. Protocol violation. Banning the peer.", headers.Count);

                validationError = "Protocol violation.";

                this.logger.LogTrace("(-)[TOO_MANY_HEADERS]:false");
                return false;
            }

            // Check headers for consecutiveness.
            for (int i = 1; i < headers.Count; i++)
            {
                headers[i - 1].PrecomputeHash(true, true);

                if (headers[i].HashPrevBlock != headers[i - 1].GetHash())
                {
                    this.logger.LogDebug("Peer '{0}' presented non-consecutiveness hashes at position {1} with prev hash '{2}' not matching hash '{3}'.",
                        peer.RemoteSocketEndpoint, i, headers[i].HashPrevBlock, headers[i - 1].GetHash());

                    validationError = "Peer presented nonconsecutive headers.";

                    this.logger.LogTrace("(-)[NONCONSECUTIVE]:false");
                    return false;
                }
            }

            headers[headers.Count - 1].PrecomputeHash(true, true); // cache the last hash as well

            return true;
        }

        /// <summary>Presents the headers to <see cref="ConsensusManager"/> and handles exceptions if any.</summary>
        /// <remarks>Have to be locked by <see cref="asyncLock"/>.</remarks>
        /// <param name="headers">List of headers that the peer presented.</param>
        /// <param name="triggerDownload">Specifies if the download should be scheduled for interesting blocks.</param>
        protected async Task<ConnectNewHeadersResult> PresentHeadersLockedAsync(List<BlockHeader> headers, bool triggerDownload = true)
        {
            ConnectNewHeadersResult result = null;

            INetworkPeer peer = this.AttachedPeer;

            if (peer == null)
            {
                this.logger.LogDebug("Peer detached!");
                this.logger.LogTrace("(-)[PEER_DETACHED]:null");
                return null;
            }

            try
            {
                result = this.consensusManager.HeadersPresented(peer, headers, triggerDownload);
            }
            catch (ConnectHeaderException)
            {
                // This is typically thrown when the first header refers to a previous block hash that is not in
                // the header tree currently. This is not regarded as bannable, as it could be a legitimate reorg.
                this.logger.LogDebug("Unable to connect headers.");

                if (this.CheckIfUnsolicitedFutureHeader(headers))
                {
                    // However, during IBD it is much more likely that the unconnectable header is an unsolicited
                    // block advertisement. In that case we just ignore the failed header and don't modify the cache.
                    // TODO: Review more closely what Bitcoin Core does here - they seem to allow 8 unconnectable headers before
                    // applying partial DoS points to a peer. Incorporate that into rate limiting code?
                    this.logger.LogTrace("(-)[HEADER_FUTURE_CANT_CONNECT_2]");
                }
                else
                {
                    this.cachedHeaders.Clear();

                    // Resync in case we can't connect the header.
                    await this.ResyncAsync().ConfigureAwait(false);
                }
            }
            catch (ConsensusRuleException exception)
            {
                this.logger.LogDebug("Peer's header is invalid. Peer will be banned and disconnected. Error: {0}.", exception.ConsensusError);
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, $"Peer presented invalid header, error: {exception.ConsensusError}.");
            }
            catch (HeaderInvalidException)
            {
                this.logger.LogDebug("Peer's header is invalid. Peer will be banned and disconnected.");
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, $"Peer presented invalid header.");
            }
            catch (CheckpointMismatchException)
            {
                this.logger.LogDebug("Peer's headers violated a checkpoint. Peer will be banned and disconnected.");
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, "Peer presented header that violates a checkpoint.");
            }
            catch (MaxReorgViolationException)
            {
                this.logger.LogDebug("Peer violates max reorg. Peer will be banned and disconnected.");
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, "Peer violates max reorg rule.");
            }

            return result;
        }

        private bool CheckIfUnsolicitedFutureHeader(List<BlockHeader> headers)
        {
            if (headers.Count == 1 && this.BestReceivedTip != null)
            {
                // Distance of header from the peer expected tip.
                double distanceSeconds = (headers[0].BlockTime - this.BestReceivedTip.Header.BlockTime).TotalSeconds;

                if (this.chain.Network.MaxTipAge < distanceSeconds)
                {
                    // A single header that is not connected to last header is likely an
                    // unsolicited header that is a result of the peer tip being extended.
                    // If the header time is far in the future we ignore it.
                    return true;
                }
            }

            // If there was more than one header received, or the single received header was not far in the future,
            // then we may need to take further action.
            return false;
        }

        /// <summary>Sync when handshake is finished.</summary>
        private async Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            if (peer.State == NetworkPeerState.HandShaked)
                await this.ResyncAsync().ConfigureAwait(false);
        }

        /// <summary>Resets the expected peer tip and last sent tip and triggers synchronization.</summary>
        public async Task ResetPeerTipInformationAndSyncAsync()
        {
            this.BestReceivedTip = null;
            this.BestSentHeader = null;

            await this.ResyncAsync().ConfigureAwait(false);
        }

        /// <summary>Updates the best sent header but only if the new value is better or is on a different chain.</summary>
        /// <param name="header">The new value to set if it is better or on a different chain.</param>
        public void UpdateBestSentHeader(ChainedHeader header)
        {
            if (header == null)
            {
                this.logger.LogTrace("(-)[HEADER_NULL]");
                return;
            }

            lock (this.bestSentHeaderLock)
            {
                if (this.BestSentHeader != null)
                {
                    ChainedHeader ancestorOrSelf = this.BestSentHeader.FindAncestorOrSelf(header);

                    if (ancestorOrSelf == header)
                    {
                        // Header is on the same chain and is behind or it is the same as the current best header.
                        this.logger.LogTrace("(-)[HEADER_BEHIND_OR_SAME]");
                        return;
                    }
                }

                this.BestSentHeader = header;
            }
        }

        /// <summary>Tries to sync the chain with the peer by sending it <see cref="GetHeadersPayload"/> in case peer's state is <see cref="NetworkPeerState.HandShaked"/>.</summary>
        public async Task ResyncAsync()
        {
            INetworkPeer peer = this.AttachedPeer;

            if ((peer != null) && (peer.State == NetworkPeerState.HandShaked))
            {
                GetHeadersPayload getHeadersPayload = this.BuildGetHeadersPayload();
                if (getHeadersPayload == null)
                {
                    this.logger.LogDebug("Ignoring sync request, headersPayload is null.");
                    return;
                }

                try
                {
                    this.logger.LogDebug("Sending getheaders payload with first hash: '{0}'.", getHeadersPayload.BlockLocator.Blocks.First());

                    peer.SendMessage(getHeadersPayload);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebug("Unable to send getheaders ({0}) message to peer '{1}'.", getHeadersPayload.GetType().Name, peer.RemoteSocketEndpoint);
                }
            }
            else
                this.logger.LogDebug("Can't sync. Peer's state is not handshaked or peer was not attached.");
        }

        /// <summary>
        /// Builds the GetHeadersPayload.
        /// </summary>
        /// <returns>The GetHeadersPayload instance. May return <c>null</c>; in such case the sync process wouldn't happen.</returns>
        protected virtual GetHeadersPayload BuildGetHeadersPayload()
        {
            return new GetHeadersPayload()
            {
                BlockLocator = (this.BestReceivedTip ?? this.consensusManager.Tip).GetLocator(),
                HashStop = null
            };
        }

        /// <summary>
        /// Processes "inv" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="inventory">List of inventory vectors to process.</param>
        /// <remarks>
        /// The "inv" message is sent unsolicited by legacy nodes to announce new blocks.
        /// It may contain non-block hashes which should be ignored. We then need to obtain the
        /// headers of the announced blocks so that the header processing can be performed as
        /// normal. We do not request the block directly as the peer may be attempting a DoS
        /// attack by spamming fake inv messages.
        /// </remarks>
        protected async Task ProcessInvAsync(INetworkPeer peer, List<InventoryVector> inventory)
        {
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogTrace("(-)[IGNORE_DURING_IBD]");
                return;
            }

            if (inventory.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_INVENTORY]");
                return;
            }

            if (inventory.Count > InvPayload.MaxInventorySize)
            {
                this.logger.LogDebug("Peer sent oversize inventory message. Peer will be banned and disconnected.");
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, "Peer sent oversize inventory message.");
                this.logger.LogTrace("(-)[OVERSIZE_INVENTORY]");
                return;
            }

            // There is no point sending getheaders for every MSG_BLOCK announced. Just send one if needed.
            bool blockHashAnnounced = false;
            foreach (InventoryVector inventoryVector in inventory)
            {
                if ((inventoryVector.Type == InventoryType.MSG_BLOCK) && (this.chain.GetBlock(inventoryVector.Hash) == null))
                {
                    blockHashAnnounced = true;
                    break;
                }
            }

            if (blockHashAnnounced)
            {
                this.logger.LogDebug("Block was announced, sending getheaders.");
                await this.ResyncAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            // Initialize auto sync timer.
            int interval = (int)TimeSpan.FromMinutes(AutosyncIntervalMinutes).TotalMilliseconds;
            this.autosyncTimer = new Timer(async (o) =>
            {
                await this.ResyncAsync().ConfigureAwait(false);
            }, null, interval, interval);

            if (this.AttachedPeer.State == NetworkPeerState.Connected)
                this.AttachedPeer.MyVersion.StartHeight = this.consensusManager.Tip.Height;

            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.autosyncTimer?.Dispose();

            base.Dispose();
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory);
        }

        internal int GetCachedItemsCount()
        {
            return this.cachedHeaders.Count;
        }

        /// <summary>
        /// Gets the count of consumed headers in a <paramref name="headers"/> list, giving a <paramref name="consumedHeader"/> reference.
        /// All items up to <paramref name="consumedHeader"/> are considered consumed.
        /// </summary>
        /// <param name="headers">List of headers to use to get the consumed count.</param>
        /// <param name="consumedHeader">The consumed header reference.</param>
        /// <returns>The number of consumed cached items.</returns>
        private int GetConsumedHeadersCount(List<BlockHeader> headers, BlockHeader consumedHeader)
        {
            uint256 consumedHeaderHash = consumedHeader.GetHash();

            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].GetHash() == consumedHeaderHash)
                {
                    return i + 1;
                }
            }

            return 0;
        }
    }
}