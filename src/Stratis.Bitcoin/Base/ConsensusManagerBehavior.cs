using System;
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
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <inheritdoc cref="ConsensusManager"/>
        private readonly ConsensusManager consensusManager;

        /// <summary>
        /// Our view of the peer's consensus tip constructed on peer's announcement of its tip using "headers" message.
        /// </summary>
        /// <remarks>
        /// The announced tip is accepted if it seems to be valid. Validation is only done on headers and so the announced tip may refer to invalid block.
        /// </remarks>
        public ChainedHeader ExpectedTip { get; private set; }

        private Timer refreshTimer;

        public ConsensusManagerBehavior(IInitialBlockDownloadState initialBlockDownloadState, ConsensusManager consensusManager, ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.refreshTimer = new Timer(async (o) =>
            {
                this.logger.LogTrace("()");

                try
                {
                    await this.TrySyncAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

                this.logger.LogTrace("(-)");
            }, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);

            if (this.AttachedPeer.State == NetworkPeerState.Connected)
                this.AttachedPeer.MyVersion.StartHeight = this.consensusManager.Tip?.Height ?? 0;

            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);

            this.logger.LogTrace("(-)");
        }

        public ConnectNewHeadersResult ConsensusTipChanged(ChainedHeader chainedHeader)
        {
            // TODO async lock has to be obtained before calling CM.HeadersPresented
            // TODO Call CM.HeadersPresented with (false) and return ConnectNewHeadersResult
            // TODO clear the cached when peer disconnects

            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            try
            {
                switch (message.Message.Payload)
                {
                    case InvPayload inv:
                        await this.ProcessInvAsync(inv).ConfigureAwait(false);
                        break;

                    case GetHeadersPayload getHeaders:
                        await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                        break;

                    case HeadersPayload headers:
                        this.ProcessHeaders(peer, headers);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes "inv" message received from the peer.
        /// </summary>
        /// <param name="invPayload">Payload of "inv" message to process.</param>
        private async Task ProcessInvAsync(InvPayload invPayload)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(invPayload), invPayload);

            // Sync in case a peer advertises us a block we are not aware of.
            if (invPayload.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !this.chain.Contains(i.Hash)))
            {
                await this.TrySyncAsync().ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes "getheaders" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="getHeadersPayload">Payload of "getheaders" message to process.</param>
        /// <remarks>
        /// "getheaders" message is sent by the peer in response to "inv(block)" message
        /// after the connection is established, or in response to "headers" message
        /// until an empty array is returned.
        /// <para>
        /// This payload notifies peers of our current best validated height,
        /// which is held by consensus tip (not concurrent chain tip).
        /// </para>
        /// <para>
        /// If the peer is behind/equal to our best height an empty array is sent back.
        /// </para>
        /// </remarks>
        private async Task ProcessGetHeadersAsync(INetworkPeer peer, GetHeadersPayload getHeadersPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(getHeadersPayload), getHeadersPayload);

            // Ignoring "getheaders" from peers because node is in initial block download unless the peer is whitelisted.
            // Don't tell peers at which stage of syncing with the network node is, don't announce which blocks we have.
            if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.Behavior<ConnectionManagerBehavior>().Whitelisted)
            {
                this.logger.LogTrace("(-)[IGNORE_ON_IBD]");
                return;
            }

            var headers = new HeadersPayload();
            ChainedHeader consensusTip = this.consensusManager.Tip;
            consensusTip = this.chain.GetBlock(consensusTip.HashBlock);

            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocators);
            if (fork != null)
            {
                if ((consensusTip == null) || (fork.Height > consensusTip.Height))
                {
                    // Fork not yet validated.
                    fork = null;
                }

                if (fork != null)
                {
                    foreach (ChainedHeader header in this.chain.EnumerateToTip(fork).Skip(1))
                    {
                        if (header.Height > consensusTip.Height)
                            break;

                        headers.Headers.Add(header.Header);
                        if ((header.HashBlock == getHeadersPayload.HashStop) || (headers.Headers.Count == 2000))
                            break;
                    }
                }
            }

            // Set our view of peer's tip equal to the last header that was sent to it.
            if (headers.Headers.Count != 0)
                this.ExpectedTip = this.chain.GetBlock(headers.Headers.Last().GetHash()) ?? this.ExpectedTip;

            await peer.SendMessageAsync(headers).ConfigureAwait(false);

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
        /// <para>
        /// If we receive a valid header from peer which work is higher than the work
        /// of our best chain's tip, we update our view of the best chain to that tip.
        /// </para>
        /// </remarks>
        private void ProcessHeaders(INetworkPeer peer, HeadersPayload headersPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(headersPayload), headersPayload);

            if (headersPayload.Headers.Count == 0)
            {
                this.logger.LogTrace("Headers payload with no headers was received. Assuming we're synced with the peer.");
                this.logger.LogTrace("(-)[NO_HEADERS]");
                return;
            }

            // TODO if queue is not empty- add to queue instead of calling CM.

            ConnectNewHeadersResult result = this.consensusManager.HeadersPresented(peer, headersPayload.Headers);

            // TODO Based on consumed add to queue or not

            this.logger.LogTrace("(-)");
        }

        private async Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(peer.State), peer.State);

            try
            {
                await this.TrySyncAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            this.logger.LogTrace("(-)");
        }

        public async Task ResetPendingTipAndSyncAsync()
        {
            this.logger.LogTrace("()");

            this.ExpectedTip = null;

            try
            {
                await this.TrySyncAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Tries to sync the chain with the peer by sending it "headers" message.
        /// </summary>
        private async Task TrySyncAsync()
        {
            this.logger.LogTrace("()");

            INetworkPeer peer = this.AttachedPeer;
            if (peer != null)
            {
                if (peer.State == NetworkPeerState.HandShaked)
                {
                    var headersPayload = new GetHeadersPayload()
                    {
                        BlockLocators = (this.ExpectedTip ?? this.consensusManager.Tip).GetLocator(),
                        HashStop = null
                    };

                    await peer.SendMessageAsync(headersPayload).ConfigureAwait(false);
                }
                else
                    this.logger.LogTrace("No sync. Peer's state is not handshaked.");
            }
            else this.logger.LogTrace("No peer attached.");

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);

            this.consensusManager.PeerDisconnected(this.AttachedPeer.Connection.Id);

            this.logger.LogTrace("(-)");
        }

        ///  <inheritdoc />
        public override void Dispose()
        {
            this.refreshTimer?.Dispose();

            base.Dispose();
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ConsensusManagerBehavior(this.initialBlockDownloadState, this.consensusManager, this.loggerFactory);
        }
    }
}
