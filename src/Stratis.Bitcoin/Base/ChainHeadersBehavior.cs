using System;
using System.Linq;
using System.Threading;
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
        private readonly ChainState chainState;

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

        /// <summary>
        /// Initializes an instanse of the object.
        /// </summary>
        /// <param name="chain">Thread safe chain of block headers from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        public ChainHeadersBehavior(ConcurrentChain chain, ChainState chainState, IInitialBlockDownloadState initialBlockDownloadState, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(chain, nameof(chain));

            this.chainState = chainState;
            this.chain = chain;
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");

            this.AutoSync = true;
            this.CanSync = true;
            this.CanRespondToGetHeaders = true;
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.refreshTimer = new Timer(o =>
            {
                this.logger.LogTrace("()");

                if (this.AutoSync)
                    this.TrySync();

                this.logger.LogTrace("(-)");
            }, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);

            this.RegisterDisposable(this.refreshTimer);
            if (this.AttachedPeer.State == NetworkPeerState.Connected)
            {
                ChainedBlock highPoW = this.chainState.ConsensusTip;
                this.AttachedPeer.MyVersion.StartHeight = highPoW?.Height ?? 0;
            }

            this.AttachedPeer.StateChanged += this.AttachedPeer_StateChanged;

            // TODO: Previously, this has been implemented using filters, which guaranteed
            // that ChainHeadersBehavior will be first to be notified about the message.
            // This is no longer EXPLICITLY guaranteed with event approach,
            // and the order of notifications only depends on the order of component
            // subscription. When we refactor the events, we should make sure ChainHeadersBehavior
            // is first to go again.
            //
            // To guarantee that priority for ChainHeadersBehavior until events are refactored
            // we use special MessageReceivedPriority now instead of normal MessageReceived event.
            this.AttachedPeer.MessageReceivedPriority += this.AttachedPeer_MessageReceived;

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceivedPriority -= this.AttachedPeer_MessageReceived;
            this.AttachedPeer.StateChanged -= this.AttachedPeer_StateChanged;

            this.logger.LogTrace("(-)");
        }

        private void AttachedPeer_MessageReceived(NetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            switch (message.Message.Payload)
            {
                case InvPayload inv:
                    {
                        if (inv.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !this.Chain.Contains(i.Hash)))
                        {
                            // No need of periodical refresh, the peer is notifying us.
                            this.refreshTimer.Dispose();
                            if (this.AutoSync)
                                this.TrySync();
                        }
                        break;
                    }

                case GetHeadersPayload getHeaders:
                    {
                        // Represents our height from the peer's point of view.
                        // It is sent from the peer on first connect, in response to Inv(Block)
                        // or in response to HeaderPayload until an empty array is returned.
                        // This payload notifies peers of our current best validated height.
                        // Use the ChainState.ConsensusTip property (not Chain.Tip)
                        // if the peer is behind/equal to our best height an empty array is sent back.

                        if (!this.CanRespondToGetHeaders) break;

                        // Ignoring "getheaders" from peers because node is in initial block download.
                        // If not in IBD whitelisted won't be checked.
                        if (this.initialBlockDownloadState.IsInitialBlockDownload() && !peer.Behavior<ConnectionManagerBehavior>().Whitelisted) break;

                        HeadersPayload headers = new HeadersPayload();
                        ChainedBlock consensusTip = this.chainState.ConsensusTip;
                        consensusTip = this.Chain.GetBlock(consensusTip.HashBlock);

                        ChainedBlock fork = this.Chain.FindFork(getHeaders.BlockLocators);
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
                                    if ((header.HashBlock == getHeaders.HashStop) || (headers.Headers.Count == 2000))
                                        break;
                                }
                            }
                        }

                        // Set our view of peer's tip equal to the last header that was sent to it.
                        if (headers.Headers.Count != 0)
                            this.pendingTip = this.Chain.GetBlock(headers.Headers.Last().GetHash()) ?? this.pendingTip;

                        peer.SendMessageVoidAsync(headers);
                        break;
                    }

                case HeadersPayload newHeaders:
                    {
                        // Represents the peers height from our point view.
                        // This updates the pending tip parameter which is
                        // the peers current best validated height.
                        // If the peer's height is higher Chain.Tip is updated to have
                        // the most PoW header.
                        // It is sent in response to GetHeadersPayload or is solicited by the
                        // peer when a new block is validated (and not in IBD).

                        if (!this.CanSync) break;

                        ChainedBlock pendingTipBefore = this.GetPendingTipOrChainTip();
                        this.logger.LogTrace("Pending tip is '{0}', received {1} new headers.", pendingTipBefore, newHeaders.Headers.Count);

                        // TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

                        ChainedBlock tip = pendingTipBefore;
                        foreach (BlockHeader header in newHeaders.Headers)
                        {
                            ChainedBlock prev = tip.FindAncestorOrSelf(header.HashPrevBlock);
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
                                    var getHeadersPayload = new GetHeadersPayload()
                                    {
                                        BlockLocators = pendingTipBefore.GetLocator(),
                                        HashStop = null
                                    };

                                    peer.SendMessageVoidAsync(getHeadersPayload);
                                    break;
                                }

                                // Now we know the previous block header and thus we can connect the new header.
                            }

                            tip = new ChainedBlock(header, header.GetHash(peer.Network.NetworkOptions), prev);
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

                        if ((this.pendingTip != null) && (this.pendingTip.ChainWork > this.Chain.Tip.ChainWork))
                        {
                            // Long reorganization protection on POS networks.
                            bool reorgPrevented = false;
                            uint maxReorgLength = this.chainState.MaxReorgLength;
                            if (maxReorgLength != 0)
                            {
                                Network network = peer.Network;
                                ChainedBlock consensusTip = this.chainState.ConsensusTip;
                                if ((network != null) && (consensusTip != null))
                                {
                                    ChainedBlock fork = this.pendingTip.FindFork(consensusTip);
                                    if ((fork != null) && (fork != consensusTip))
                                    {
                                        int reorgLength = consensusTip.Height - fork.Height;
                                        if (reorgLength > maxReorgLength)
                                        {
                                            this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}'.", reorgLength, maxReorgLength, consensusTip);
                                            this.InvalidHeaderReceived = true;
                                            reorgPrevented = true;
                                        }
                                        else this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                                    }
                                }
                            }

                            // Switch to better chain.
                            if (!reorgPrevented)
                            {
                                this.logger.LogTrace("New chain tip '{0}' selected, chain work is '{1}'.", this.pendingTip, this.pendingTip.ChainWork);
                                this.Chain.SetTip(this.pendingTip);
                            }
                        }

                        ChainedBlock chainedPendingTip = this.pendingTip == null ? null : this.Chain.GetBlock(this.pendingTip.HashBlock);
                        if (chainedPendingTip != null)
                        {
                            // This allows garbage collection to collect the duplicated pendingTip and ancestors.
                            this.pendingTip = chainedPendingTip;
                        }

                        if ((!this.InvalidHeaderReceived) && (newHeaders.Headers.Count != 0) && (pendingTipBefore.HashBlock != this.GetPendingTipOrChainTip().HashBlock))
                            this.TrySync();

                        break;
                    }
            }

            this.logger.LogTrace("(-)");
        }

        public void SetPendingTip(ChainedBlock newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            uint256 pendingTipChainWork = this.PendingTip.ChainWork;
            if (newTip.ChainWork > pendingTipChainWork)
            {
                ChainedBlock chainedPendingTip = this.Chain.GetBlock(newTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    // This allows garbage collection to collect the duplicated pendingtip and ancestors.
                    this.pendingTip = chainedPendingTip;
                }
            }
            else this.logger.LogTrace("New pending tip not set because its chain work '{0}' is lower than current's pending tip's chain work '{1}'.", newTip.ChainWork, pendingTipChainWork);

            this.logger.LogTrace("(-)");
        }

        private void AttachedPeer_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(peer), peer.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(peer.State), peer.State);

            this.TrySync();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Asynchronously try to sync the chain.
        /// </summary>
        public void TrySync()
        {
            this.logger.LogTrace("()");

            NetworkPeer peer = this.AttachedPeer;
            if (peer != null)
            {
                if ((peer.State == NetworkPeerState.HandShaked) && this.CanSync && !this.InvalidHeaderReceived)
                {
                    peer.SendMessageVoidAsync(new GetHeadersPayload()
                    {
                        BlockLocators = this.GetPendingTipOrChainTip().GetLocator()
                    });
                }
                else this.logger.LogTrace("No sync. Peer's state is {0} (need {1}), {2} sync, {3}invalid header received from this peer.", peer.State, NetworkPeerState.HandShaked, this.CanSync ? "CAN" : "CAN'T", this.InvalidHeaderReceived ? "" : "NO ");
            }
            else this.logger.LogTrace("No peer attached.");

            this.logger.LogTrace("(-)");
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

        public ChainedBlock GetPendingTipOrChainTip()
        {
            return this.pendingTip ?? this.chainState.ConsensusTip ?? this.Chain.Tip;
        }

        public override object Clone()
        {
            var clone = new ChainHeadersBehavior(this.Chain, this.chainState, this.initialBlockDownloadState, this.loggerFactory)
            {
                CanSync = this.CanSync,
                CanRespondToGetHeaders = this.CanRespondToGetHeaders,
                AutoSync = this.AutoSync,
            };
            return clone;
        }
    }
}
