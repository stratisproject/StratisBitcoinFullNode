using System;
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

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// The Chain Behavior is responsible for keeping a ConcurrentChain up to date with the peer, it also responds to getheaders messages.
    /// </summary>
    public class ChainHeadersBehavior : NetworkPeerBehavior
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary><c>true</c> if the chain should be kept in sync, <c>false</c> otherwise.</summary>
        public bool CanSync { get; set; }

        /// <summary><c>true</c> to sync the chain as headers come from the network, <c>false</c> not to sync automatically.</summary>
        public bool AutoSync { get; set; }

        /// <summary>
        /// Our view of the peer's headers tip constructed on peer's announcement of its tip using "headers" message.
        /// <para>
        /// The announced tip is accepted if it seems to be valid. Validation is only done on headers
        /// and so the announced tip may refer to invalid block.
        /// </para>
        /// </summary>
        /// <remarks>It might be different than concurrent's chain tip, in the rare event of large fork > 2000 blocks.</remarks>
        private ChainedBlock pendingTip;

        /// <summary>Information about the peer's announcement of its tip using "headers" message.</summary>
        public ChainedBlock PendingTip
        {
            get
            {
                ChainedBlock tip = this.pendingTip;
                if (tip == null)
                    return null;

                // Prevent memory leak by returning a block from the chain instead of real pending tip if possible.
                return this.Chain.GetBlock(tip.HashBlock) ?? tip;
            }
        }

        /// <summary><c>true</c> to respond to "getheaders" messages, <c>false</c> to ignore it.</summary>
        public bool CanRespondToGetHeaders { get; set; }

        private Timer refreshTimer;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private ConcurrentChain chain;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        public ConcurrentChain Chain
        {
            get
            {
                return this.chain;
            }
            set
            {
                this.AssertNotAttached();
                this.chain = value;
            }
        }

        public bool InvalidHeaderReceived { get; private set; }

        /// <summary>Selects the best available chain based on tips provided by the peers and switches to it.</summary>
        private readonly BestChainSelector bestChainSelector;

        /// <summary>
        /// Initializes an instanse of the object.
        /// </summary>
        /// <param name="chain">Thread safe chain of block headers from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        /// <param name="bestChainSelector">Selects the best available chain based on tips provided by the peers and switches to it.</param>
        public ChainHeadersBehavior(ConcurrentChain chain, IChainState chainState, IInitialBlockDownloadState initialBlockDownloadState, BestChainSelector bestChainSelector, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(chain, nameof(chain));

            this.chainState = chainState;
            this.chain = chain;
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.bestChainSelector = bestChainSelector;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");

            this.AutoSync = true;
            this.CanSync = true;
            this.CanRespondToGetHeaders = true;
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.refreshTimer = new Timer(async (o) =>
            {
                this.logger.LogTrace("()");

                try
                {
                    if (this.AutoSync)
                        await this.TrySyncAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

                this.logger.LogTrace("(-)");
            }, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);

            this.RegisterDisposable(this.refreshTimer);
            if (this.AttachedPeer.State == NetworkPeerState.Connected)
            {
                ChainedBlock highPoW = this.chainState.ConsensusTip;
                this.AttachedPeer.MyVersion.StartHeight = highPoW?.Height ?? 0;
            }

            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);
            
            this.bestChainSelector.RemoveAvailableTip(this.AttachedPeer.Connection.Id);

            this.logger.LogTrace("(-)");
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
                        await this.ProcessHeadersAsync(peer, headers).ConfigureAwait(false);
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

            if (invPayload.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !this.Chain.Contains(i.Hash)))
            {
                // No need of periodical refresh, the peer is notifying us.
                this.refreshTimer.Dispose();
                if (this.AutoSync)
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

            if (!this.CanRespondToGetHeaders)
            {
                this.logger.LogTrace("(-)[CANT_RESPOND_TO_HEADERS]");
                return;
            }

            // Ignoring "getheaders" from peers because node is in initial block download unless the peer is whitelisted.
            if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.Behavior<ConnectionManagerBehavior>().Whitelisted)
            {
                this.logger.LogTrace("(-)[IGNORE_ON_IBD]");
                return;
            }

            HeadersPayload headers = new HeadersPayload();
            ChainedBlock consensusTip = this.chainState.ConsensusTip;
            consensusTip = this.Chain.GetBlock(consensusTip.HashBlock);

            ChainedBlock fork = this.Chain.FindFork(getHeadersPayload.BlockLocators);
            if (fork != null)
            {
                if ((consensusTip == null) || (fork.Height > consensusTip.Height))
                {
                    // Fork not yet validated.
                    fork = null;
                }

                if (fork != null)
                {
                    foreach (ChainedBlock header in this.Chain.EnumerateToTip(fork).Skip(1))
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
                this.pendingTip = this.Chain.GetBlock(headers.Headers.Last().GetHash()) ?? this.pendingTip;

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
        private async Task ProcessHeadersAsync(INetworkPeer peer, HeadersPayload headersPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(headersPayload), headersPayload);

            if (!this.CanSync)
            {
                this.logger.LogTrace("(-)[CANT_SYNC]");
                return;
            }

            if (headersPayload.Headers.Count == 0)
            {
                this.logger.LogTrace("Headers payload with no headers was received. Assuming we're synced with the peer.");
                this.logger.LogTrace("(-)[NO_HEADERS]");
                return;
            }

            ChainedBlock pendingTipBefore = this.pendingTip;
            this.logger.LogTrace("Pending tip is '{0}', received {1} new headers.", pendingTipBefore, headersPayload.Headers.Count);

            bool doTrySync = false;

            // TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

            ChainedBlock tip = pendingTipBefore;
            foreach (BlockHeader header in headersPayload.Headers)
            {
                ChainedBlock prev = tip?.FindAncestorOrSelf(header.HashPrevBlock);
                if (prev == null)
                {
                    this.logger.LogTrace("Previous header of the new header '{0}' was not found on the peer's chain, the view of the peer's chain is probably outdated.", header);

                    // We have received a header from the peer for which we don't register a previous header.
                    // This can happen if our information about where the peer is is invalid.
                    // However, if the previous header is on the chain that we recognize,
                    // we can fix it.

                    // Try to find the header's previous hash on our best chain.
                    prev = this.Chain.GetBlock(header.HashPrevBlock);

                    if (prev == null)
                    {
                        this.logger.LogTrace("Previous header of the new header '{0}' was not found on our chain either.", header);

                        // If we can't connect the header we received from the peer, we might be on completely different chain or
                        // a reorg happened recently. If we ignored it, we would have invalid view of the peer and the propagation
                        // of blocks would not work well. So we ask the peer for headers using "getheaders" message.
                        // Enforce a sync.
                        doTrySync = true;
                        break;
                    }

                    // Now we know the previous block header and thus we can connect the new header.
                }

                tip = new ChainedBlock(header, header.GetHash(), prev);
                bool validated = this.Chain.GetBlock(tip.HashBlock) != null || tip.Validate(peer.Network);
                validated &= !this.chainState.IsMarkedInvalid(tip.HashBlock);
                if (!validated)
                {
                    this.logger.LogTrace("Validation of new header '{0}' failed.", tip);
                    this.InvalidHeaderReceived = true;
                    break;
                }

                this.pendingTip = tip;
            }

            if (pendingTipBefore != this.pendingTip)
                this.logger.LogTrace("Pending tip changed to '{0}'.", this.pendingTip);

            if ((this.pendingTip != null) && !this.bestChainSelector.TrySetAvailableTip(this.AttachedPeer.Connection.Id, this.pendingTip))
                this.InvalidHeaderReceived = true;
            
            ChainedBlock chainedPendingTip = this.pendingTip == null ? null : this.Chain.GetBlock(this.pendingTip.HashBlock);
            if (chainedPendingTip != null)
            {
                // This allows garbage collection to collect the duplicated pendingTip and ancestors.
                this.pendingTip = chainedPendingTip;
            }

            // If we made any advancement or the sync is enforced by 'doTrySync'- continue syncing.
            if (doTrySync || (this.pendingTip == null) || (pendingTipBefore == null) || (pendingTipBefore.HashBlock != this.pendingTip.HashBlock))
                await this.TrySyncAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        public void SetPendingTip(ChainedBlock newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            if ((this.PendingTip == null) || (newTip.ChainWork  > this.PendingTip.ChainWork))
            {
                ChainedBlock chainedPendingTip = this.Chain.GetBlock(newTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    // This allows garbage collection to collect the duplicated pendingtip and ancestors.
                    this.pendingTip = chainedPendingTip;
                }
            }
            else this.logger.LogTrace("New pending tip not set because its chain work '{0}' is lower than current's pending tip's chain work '{1}'.", newTip.ChainWork, this.PendingTip.ChainWork);

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

        /// <summary>
        /// Tries to sync the chain with the peer by sending it "headers" message.
        /// </summary>
        public async Task TrySyncAsync()
        {
            this.logger.LogTrace("()");

            INetworkPeer peer = this.AttachedPeer;
            if (peer != null)
            {
                if ((peer.State == NetworkPeerState.HandShaked) && this.CanSync && !this.InvalidHeaderReceived)
                {
                    await peer.SendMessageAsync(this.GetPendingTipHeadersPayload()).ConfigureAwait(false);
                }
                else this.logger.LogTrace("No sync. Peer's state is {0} (need {1}), {2} sync, {3}invalid header received from this peer.", peer.State, NetworkPeerState.HandShaked, this.CanSync ? "CAN" : "CAN'T", this.InvalidHeaderReceived ? "" : "NO ");
            }
            else this.logger.LogTrace("No peer attached.");

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Creates <see cref="GetHeadersPayload"/> using <see cref="pendingTip"/>'s or our tip's locator.
        /// </summary>
        /// <returns>Get headers payload based on peer's or our tip.</returns>
        private GetHeadersPayload GetPendingTipHeadersPayload()
        {
            return new GetHeadersPayload()
            {
                BlockLocators = (this.pendingTip ?? this.chainState.ConsensusTip ?? this.Chain.Tip).GetLocator(),
                HashStop = null
            };
        }

        /// <summary>
        /// Determines if the peer's headers are synced with ours.
        /// </summary>
        /// <remarks>
        /// It is possible that peer is in IBD even though it has all the headers so we can't assume with 100% certainty that peer is fully synced.
        /// </remarks>
        /// <returns><c>true</c> if we are synced with the peer. Otherwise, <c>false</c>.</returns>
        public bool IsSynced()
        {
            if (this.pendingTip == null)
                return false;

            return ((this.pendingTip.Height >= this.chainState.ConsensusTip.Height) &&
                    (this.pendingTip.ChainWork >= this.chainState.ConsensusTip.ChainWork));
        }

        public override object Clone()
        {
            var clone = new ChainHeadersBehavior(this.Chain, this.chainState, this.initialBlockDownloadState, this.bestChainSelector, this.loggerFactory)
            {
                CanSync = this.CanSync,
                CanRespondToGetHeaders = this.CanRespondToGetHeaders,
                AutoSync = this.AutoSync,
            };
            return clone;
        }
    }
}
