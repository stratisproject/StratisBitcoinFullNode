#if !NOSOCKET
using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// The Chain Behavior is responsible for keeping a ConcurrentChain up to date with the peer, it also responds to getheaders messages.
    /// </summary>
    public class ChainHeadersBehavior : NodeBehavior
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information about node's chain.</summary>
        private readonly ChainState chainState;

        /// <summary><c>true</c> if the chain should be kept in sync, <c>false</c> otherwise.</summary>
        public bool CanSync { get; set; }

        /// <summary><c>true</c> to sync the chain as headers come from the network, <c>false</c> not to sync automatically.</summary>
        public bool AutoSync { get; set; }

        /// <summary>
        /// Information about the peer's announcement of its tip using "headers" message.
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

                // Prevent memory leak by returning a block from the chain instead of real pending tip of possible.
                return this.Chain.GetBlock(tip.HashBlock) ?? tip;
            }
        }

        /// <summary><c>true</c> to respond to "getheaders" messages, <c>false</c> to ignore it.</summary>
        public bool CanRespondToGetHeaders { get; set; }

        private Timer refreshTimer;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private ConcurrentChain chain;
        /// <summary>Thread safe chain of block headers from genesis.</summary>
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

        private bool invalidHeaderReceived;
        public bool InvalidHeaderReceived
        {
            get
            {
                return this.invalidHeaderReceived;
            }
        }

        /// <summary>
        /// Initializes an instanse of the object.
        /// </summary>
        /// <param name="chain">Thread safe chain of block headers from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public ChainHeadersBehavior(ConcurrentChain chain, ChainState chainState, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(chain, nameof(chain));

            this.chainState = chainState;
            this.chain = chain;
            this.loggerFactory = loggerFactory;
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
            if (this.AttachedNode.State == NodeState.Connected)
            {
                ChainedBlock highPoW = this.chainState.ConsensusTip;
                this.AttachedNode.MyVersion.StartHeight = highPoW?.Height ?? 0;
            }

            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
            this.RegisterDisposable(this.AttachedNode.Filters.Add(this.Intercept));

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;

            this.logger.LogTrace("(-)");
        }

        private void Intercept(IncomingMessage message, Action act)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(message), message.Message.Command, nameof(this.AttachedNode), this.AttachedNode?.RemoteSocketEndpoint);

            var inv = message.Message.Payload as InvPayload;
            if (inv != null)
            {
                if (inv.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !this.Chain.Contains(i.Hash)))
                {
                    // No need of periodical refresh, the peer is notifying us.
                    this.refreshTimer.Dispose(); 
                    if (this.AutoSync)
                        this.TrySync();
                }
            }

            // == GetHeadersPayload ==
            // Represents our height from the peer's point of view. 
            // It is sent from the peer on first connect, in response to Inv(Block) 
            // or in response to HeaderPayload until an empty array is returned.
            // This payload notifies peers of our current best validated height. 
            // Use the ChainState.ConsensusTip property (not Chain.Tip)
            // if the peer is behind/equal to our best height an empty array is sent back.

            // Ignoring "getheaders" from peers because node is in initial block download.
            var getheaders = message.Message.Payload as GetHeadersPayload;
            if ((getheaders != null) 
                && this.CanRespondToGetHeaders
                // If not in IBD whitelisted won't be checked.
                && (!this.chainState.IsInitialBlockDownload || this.AttachedNode.Behavior<ConnectionManagerBehavior>().Whitelisted)) 
            {
                HeadersPayload headers = new HeadersPayload();
                ChainedBlock consensusTip = this.chainState.ConsensusTip;
                consensusTip = this.Chain.GetBlock(consensusTip.HashBlock);

                ChainedBlock fork = this.Chain.FindFork(getheaders.BlockLocators);
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
                            if ((header.HashBlock == getheaders.HashStop) || (headers.Headers.Count == 2000))
                                break;
                        }
                    }
                }

                this.AttachedNode.SendMessageAsync(headers);
            }

            // == HeadersPayload ==
            // Represents the peers height from our point view.
            // This updates the pending tip parameter which is 
            // the peers current best validated height.
            // If the peer's height is higher Chain.Tip is updated to have 
            // the most PoW header.
            // It is sent in response to GetHeadersPayload or is solicited by the 
            // peer when a new block is validated (and not in IBD).

            var newHeaders = message.Message.Payload as HeadersPayload;
            if ((newHeaders != null) && this.CanSync)
            {
                ChainedBlock pendingTipBefore = this.GetPendingTipOrChainTip();
                this.logger.LogTrace("Pending tip is '{0}', received {1} new headers.", pendingTipBefore, newHeaders.Headers.Count);

                // TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

                ChainedBlock tip = pendingTipBefore;
                foreach (BlockHeader header in newHeaders.Headers)
                {
                    ChainedBlock prev = tip.FindAncestorOrSelf(header.HashPrevBlock);
                    if (prev == null)
                        break;

                    tip = new ChainedBlock(header, header.GetHash(), prev);
                    bool validated = this.Chain.GetBlock(tip.HashBlock) != null || tip.Validate(this.AttachedNode.Network);
                    validated &= !this.chainState.IsMarkedInvalid(tip.HashBlock);
                    if (!validated)
                    {
                        this.logger.LogTrace("Validation of new header '{0}' failed.", tip);
                        this.invalidHeaderReceived = true;
                        break;
                    }

                    this.pendingTip = tip;
                }

                if (pendingTipBefore != this.pendingTip)
                    this.logger.LogTrace("Pending tip changed to '{0}'.", this.pendingTip);

                // Long reorganization protection on POS networks.
                bool reorgPrevented = false;
                uint maxReorgLength = this.chainState.MaxReorgLength;
                if (maxReorgLength != 0)
                {
                    Network network = this.AttachedNode?.Network;
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
                                this.invalidHeaderReceived = true;
                                reorgPrevented = true;
                            }
                            else this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                        }
                    }
                }

                if (!reorgPrevented && (this.pendingTip.ChainWork > this.Chain.Tip.ChainWork))
                {
                    this.logger.LogTrace("New chain tip '{0}' selected, chain work is '{1}'.", this.pendingTip, this.pendingTip.ChainWork);
                    this.Chain.SetTip(this.pendingTip);
                }

                ChainedBlock chainedPendingTip = this.Chain.GetBlock(this.pendingTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    // This allows garbage collection to collect the duplicated pendingTip and ancestors.
                    this.pendingTip = chainedPendingTip; 
                }

                if ((newHeaders.Headers.Count != 0) && (pendingTipBefore.HashBlock != this.GetPendingTipOrChainTip().HashBlock))
                    this.TrySync();
            }

            act();

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

        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(node), node.RemoteSocketEndpoint, nameof(oldState), oldState, nameof(node.State), node.State);

            this.TrySync();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Asynchronously try to sync the chain.
        /// </summary>
        public void TrySync()
        {
            this.logger.LogTrace("()");

            Node node = this.AttachedNode;
            if (node != null)
            {
                if ((node.State == NodeState.HandShaked) && this.CanSync && !this.invalidHeaderReceived)
                {
                    node.SendMessageAsync(new GetHeadersPayload()
                    {
                        BlockLocators = this.GetPendingTipOrChainTip().GetLocator()
                    });
                }
                else this.logger.LogTrace("No sync. Peer node's state is {0} (need {1}), {2} sync, {3}invalid header received from this peer.", node.State, NodeState.HandShaked, this.CanSync ? "CAN" : "CAN'T", this.invalidHeaderReceived ? "" : "NO ");
            }
            else this.logger.LogTrace("No node attached.");

            this.logger.LogTrace("(-)");
        }

        private ChainedBlock GetPendingTipOrChainTip()
        {
            this.pendingTip = this.pendingTip ?? this.chainState.ConsensusTip ?? this.Chain.Tip;
            return this.pendingTip;
        }

        #region ICloneable Members

        public override object Clone()
        {
            var clone = new ChainHeadersBehavior(this.Chain, this.chainState, this.loggerFactory)
            {
                CanSync = this.CanSync,
                CanRespondToGetHeaders = this.CanRespondToGetHeaders,
                AutoSync = this.AutoSync,
            };
            return clone;
        }

        #endregion
    }
}
#endif