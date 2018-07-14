﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>Behavior that takes care of headers protocol. It also keeps the notion of peer's consensus tip.</summary>
    public class ConsensusManagerBehavior : NetworkPeerBehavior
    {
        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <inheritdoc cref="ConsensusManager"/>
        private readonly ConsensusManager consensusManager;

        /// <inheritdoc cref="ConcurrentChain"/>
        private readonly ConcurrentChain chain;

        /// <inheritdoc cref="IConnectionManager"/>
        private readonly IConnectionManager connectionManager;

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
        public ChainedHeader ExpectedPeerTip { get; private set; }

        /// <summary>Timer that periodically tries to sync.</summary>
        private Timer autosyncTimer;

        /// <summary>Interval in minutes for the <see cref="autosyncTimer"/>.</summary>
        private const int AutosyncIntervalMinutes = 10;

        /// <summary>Amount of headers that should be cached until we stop syncing from the peer.</summary>
        private const int CacheSyncHeadersThreshold = 2000;

        /// <summary>Maximum number of headers in <see cref="HeadersPayload"/> according to Bitcoin protocol.</summary>
        /// <seealso cref="https://en.bitcoin.it/wiki/Protocol_documentation#getheaders"/>
        private const int MaxItemsPerHeadersMessage = 2000;

        /// <summary>List of block headers that were not yet consumed by <see cref="ConsensusManager"/>.</summary>
        /// <remarks>Should be protected by <see cref="asyncLock"/>.</remarks>
        private readonly List<BlockHeader> cachedHeaders;

        /// <summary>Protects access to <see cref="cachedHeaders"/>.</summary>
        private readonly AsyncLock asyncLock;

        public ConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState, ConsensusManager consensusManager, IPeerBanning peerBanning, IConnectionManager connectionManager, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.chain = chain;
            this.connectionManager = connectionManager;
            this.peerBanning = peerBanning;

            this.cachedHeaders = new List<BlockHeader>();
            this.asyncLock = new AsyncLock();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <summary>Presents cached headers to <see cref="ConsensusManager"/> from the cache if any and removes consumed from the cache.</summary>
        /// <param name="newTip">New consensus tip.</param>
        public async Task<ConnectNewHeadersResult> ConsensusTipChangedAsync(ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            ConnectNewHeadersResult result = null;
            bool syncRequired = false;

            using (await this.asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (this.cachedHeaders.Count != 0)
                {
                    result = await this.PresentHeadersLockedAsync(this.cachedHeaders, false).ConfigureAwait(false);

                    if (result == null)
                    {
                        this.logger.LogTrace("(-)[NO_HEADERS_CONNECTED]:null");
                        return null;
                    }

                    this.ExpectedPeerTip = result.Consumed;

                    int consumedCount = this.cachedHeaders.IndexOf(result.Consumed.Header) + 1;
                    this.cachedHeaders.RemoveRange(0, consumedCount);
                    int cacheSize = this.cachedHeaders.Count;

                    this.logger.LogTrace("{0} entries were consumed from the cache, {1} items were left.", consumedCount, cacheSize);
                    syncRequired = cacheSize == 0;
                }
            }

            if (syncRequired)
                await this.ResyncAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-):'{0}'", result);
            return result;
        }

        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            switch (message.Message.Payload)
            {
                case GetHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case HeadersPayload headers:
                    await this.ProcessHeadersAsync(peer, headers).ConfigureAwait(false);
                    break;
            }

            this.logger.LogTrace("(-)");
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
        private async Task ProcessGetHeadersAsync(INetworkPeer peer, GetHeadersPayload getHeadersPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(getHeadersPayload), getHeadersPayload);

            // Ignoring "getheaders" from peers because node is in initial block download unless the peer is whitelisted.
            // We don't want to reveal our position in IBD which can be used by attacker. Also we don't won't to deliver peers any blocks
            // because that will slow down our own syncing process.
            if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.Behavior<ConnectionManagerBehavior>().Whitelisted)
            {
                this.logger.LogTrace("(-)[IGNORE_ON_IBD]");
                return;
            }

            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocators);

            if (fork != null)
            {
                var headers = new HeadersPayload();

                foreach (ChainedHeader header in this.chain.EnumerateToTip(fork).Skip(1))
                {
                    headers.Headers.Add(header.Header);

                    if ((header.HashBlock == getHeadersPayload.HashStop) || (headers.Headers.Count == MaxItemsPerHeadersMessage))
                        break;
                }

                this.logger.LogTrace("{0} headers were selected for sending, last one is '{1}'.", headers.Headers.Count, headers.Headers.Last().GetHash());

                try
                {
                    await peer.SendMessageAsync(headers).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Unable to send headers message to peer '{0}'.", peer.RemoteSocketEndpoint);
                }
            }

            this.logger.LogTrace("(-)");
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
        private async Task ProcessHeadersAsync(INetworkPeer peer, HeadersPayload headersPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(headersPayload), headersPayload);

            List<BlockHeader> headers = headersPayload.Headers;

            if (headers.Count == 0)
            {
                this.logger.LogTrace("Headers payload with no headers was received. Assuming we're synced with the peer.");
                this.logger.LogTrace("(-)[NO_HEADERS]");
                return;
            }

            // Check headers for consecutiveness.
            for (int i = 1; i < headers.Count; i++)
            {
                if (headers[i].HashPrevBlock != headers[i - 1].GetHash())
                {
                    this.logger.LogTrace("Peer '{0}' presented non-consecutiveness hashes at position {1} with prev hash '{2}' not matching hash '{3}'.", peer.RemoteSocketEndpoint, i, headers[i].HashPrevBlock, headers[i - 1].GetHash());
                    this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, this.connectionManager.ConnectionSettings.BanTimeSeconds, "Peer presented nonconsecutive headers.");

                    this.logger.LogTrace("(-)[PEER_BANNED]");
                    return;
                }
            }

            using (await this.asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (this.cachedHeaders.Count > CacheSyncHeadersThreshold)
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

                ConnectNewHeadersResult result = await this.PresentHeadersLockedAsync(headers).ConfigureAwait(false);

                if (result == null)
                {
                    this.logger.LogTrace("Processing of {0} headers failed.", headers.Count);
                    this.logger.LogTrace("(-)[PROCESSING_FAILED]");
                    return;
                }

                this.ExpectedPeerTip = result.Consumed;

                if (result.Consumed.Header != headers.Last())
                {
                    // Some headers were not consumed, add to cache.
                    int consumedCount = headers.IndexOf(result.Consumed.Header) + 1;
                    this.cachedHeaders.AddRange(headers.Skip(consumedCount));

                    this.logger.LogTrace("{0} out of {1} items were not consumed and added to cache.", headers.Count - consumedCount, headers.Count);
                }

                if (this.cachedHeaders.Count < CacheSyncHeadersThreshold)
                    await this.ResyncAsync().ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Presents the headers to <see cref="ConsensusManager"/> and handles exceptions if any.</summary>
        /// <remarks>Have to be locked by <see cref="asyncLock"/>.</remarks>
        /// <param name="headers">List of headers that the peer presented.</param>
        /// <param name="triggerDownload">Specifies if the download should be scheduled for interesting blocks.</param>
        private async Task<ConnectNewHeadersResult> PresentHeadersLockedAsync(List<BlockHeader> headers, bool triggerDownload = true)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4})", nameof(headers), nameof(headers.Count), headers.Count, nameof(triggerDownload), triggerDownload);

            ConnectNewHeadersResult result = null;

            INetworkPeer peer = this.AttachedPeer;

            if (peer == null)
            {
                this.logger.LogTrace("(-)[PEER_DETACHED]:null");
                return null;
            }

            try
            {
                result = this.consensusManager.HeadersPresented(peer, headers, triggerDownload);
            }
            catch (ConnectHeaderException)
            {
                this.logger.LogTrace("Unable to connect headers.");
                this.cachedHeaders.Clear();

                // Resync in case can't connect.
                await this.ResyncAsync().ConfigureAwait(false);
            }
            catch (CheckpointMismatchException)
            {
                this.logger.LogTrace("Peer's headers violated a checkpoint. Peer will be banned and disconnected.");
                this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, this.connectionManager.ConnectionSettings.BanTimeSeconds, "Peer presented header that violates a checkpoint.");
            }
            //TODO catch more exceptions when validator are implemented and CM.HeadersPresented can throw anything else

            this.logger.LogTrace("(-):'{0}'", result);
            return result;
        }

        /// <summary>Resyncs the peer whenever state is changed.</summary>
        private async Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(peer.State), peer.State);

            await this.ResyncAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Resets the expected peer tip and triggers synchronization.</summary>
        public async Task ResetExpectedPeerTipAndSyncAsync()
        {
            this.logger.LogTrace("()");

            this.ExpectedPeerTip = null;

            await this.ResyncAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Tries to sync the chain with the peer by sending it <see cref="GetHeadersPayload"/> in case peer's state is <see cref="NetworkPeerState.HandShaked"/>.</summary>
        private async Task ResyncAsync()
        {
            this.logger.LogTrace("()");

            INetworkPeer peer = this.AttachedPeer;

            if ((peer != null) && (peer.State == NetworkPeerState.HandShaked))
            {
                var headersPayload = new GetHeadersPayload()
                {
                    BlockLocators = (this.ExpectedPeerTip ?? this.consensusManager.Tip).GetLocator(),
                    HashStop = null
                };

                try
                {
                    await peer.SendMessageAsync(headersPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("Unable to send getheaders message to peer '{0}'.", peer.RemoteSocketEndpoint);
                }
            }
            else
                this.logger.LogTrace("Can't sync. Peer's state is not handshaked or peer was not attached.");

            this.logger.LogTrace("(-)");
        }

        ///  <inheritdoc />
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            // Initialize auto sync timer.
            this.autosyncTimer = new Timer(async (o) =>
            {
                this.logger.LogTrace("()");

                await this.ResyncAsync().ConfigureAwait(false);

                this.logger.LogTrace("(-)");
            }, null, 0, (int)TimeSpan.FromMinutes(AutosyncIntervalMinutes).TotalMilliseconds);

            if (this.AttachedPeer.State == NetworkPeerState.Connected)
                this.AttachedPeer.MyVersion.StartHeight = this.consensusManager.Tip.Height;

            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);

            this.logger.LogTrace("(-)");
        }

        ///  <inheritdoc />
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);

            this.logger.LogTrace("(-)");
        }

        ///  <inheritdoc />
        public override void Dispose()
        {
            this.autosyncTimer?.Dispose();

            base.Dispose();
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.connectionManager, this.loggerFactory);
        }
    }
}
