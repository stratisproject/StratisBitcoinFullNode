#if !NOSOCKET
using System;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// The Chain Behavior is responsible for keeping a ConcurrentChain up to date with the peer, it also responds to getheaders messages.
    /// </summary>
    public partial class ChainHeadersBehavior : NodeBehavior
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

        /// <para>
        /// The announced tip is accepted if it seems to be valid. Validation is only done on headers 
        /// and so the announced tip may refer to invalid block.
        /// </para>
        /// </summary>
        /// <remarks>Might be different than concurrent's chain tip, in the rare event of large fork > 2000 blocks.</remarks>
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

        private int syncingCount;

        /// <summary>Using for test, this might not be reliable.</summary>
        internal bool Syncing
        {
            get
            {
                return this.syncingCount != 0;
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
            this.refreshTimer = new Timer(o =>
            {
                if (this.AutoSync)
                    this.TrySync();
            }, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);

            this.RegisterDisposable(this.refreshTimer);
            if (this.AttachedNode.State == NodeState.Connected)
            {
                var highPoW = this.chainState.ConsensusTip;
                this.AttachedNode.MyVersion.StartHeight = highPoW?.Height ?? 0;
            }

            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
            this.RegisterDisposable(this.AttachedNode.Filters.Add(this.Intercept));
        }

        void Intercept(IncomingMessage message, Action act)
        {
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
                        this.invalidHeaderReceived = true;
                        break;
                    }

                    this.pendingTip = tip;
                }

                // Long reorganization protection on POS networks.
                bool reorgPrevented = false;
                uint maxReorgLength = this.chainState.MaxReorgLength;
                Network network = this.AttachedNode?.Network;
                ChainedBlock consensusTip = this.chainState.ConsensusTip;
                if ((maxReorgLength != 0) && (network != null) && (consensusTip != null))
                {
                    ChainedBlock fork = this.pendingTip.FindFork(consensusTip);
                    if ((fork != null) && (consensusTip.Height - fork.Height > maxReorgLength))
                    {
                        this.invalidHeaderReceived = true;
                        reorgPrevented = true;
                    }
                }

                if (!reorgPrevented && (this.pendingTip.ChainWork > this.Chain.Tip.ChainWork))
                {
                    this.Chain.SetTip(this.pendingTip);
                }

                ChainedBlock chainedPendingTip = this.Chain.GetBlock(this.pendingTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    // This allows garbage collection to collect the duplicated pendingTip and ancestors.
                    this.pendingTip = chainedPendingTip; 
                }

                if (newHeaders.Headers.Count != 0 && pendingTipBefore.HashBlock != this.GetPendingTipOrChainTip().HashBlock)
                    this.TrySync();

                Interlocked.Decrement(ref this.syncingCount);
            }

            act();
        }

        public void SetPendingTip(ChainedBlock newTip)
        {
            if (newTip.ChainWork > this.PendingTip.ChainWork)
            {
                ChainedBlock chainedPendingTip = this.Chain.GetBlock(newTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    // This allows garbage collection to collect the duplicated pendingtip and ancestors.
                    this.pendingTip = chainedPendingTip;
                }
            }
        }

        /// <summary>
        /// Check if any past blocks announced by this peer is in the invalid blocks list, and set InvalidHeaderReceived flag accordingly.
        /// </summary>
        /// <returns>True if no invalid block is received</returns>
        public bool CheckAnnouncedBlocks()
        {
            ChainedBlock tip = this.pendingTip;

            if ((tip != null) && !this.invalidHeaderReceived)
            {
                try
                {
                    this.chainState.invalidBlocksLock.EnterReadLock();
                    if (this.chainState.invalidBlocks.Count != 0)
                    {
                        foreach (ChainedBlock header in tip.EnumerateToGenesis())
                        {
                            if (this.invalidHeaderReceived)
                                break;

                            this.invalidHeaderReceived |= this.chainState.invalidBlocks.Contains(header.HashBlock);
                        }
                    }
                }
                finally
                {
                    this.chainState.invalidBlocksLock.ExitReadLock();
                }
            }

            return !this.invalidHeaderReceived;
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            this.TrySync();
        }

        /// <summary>
        /// Asynchronously try to sync the chain.
        /// </summary>
        public void TrySync()
        {
            Node node = this.AttachedNode;
            if (node != null)
            {
                if ((node.State == NodeState.HandShaked) && this.CanSync && !this.invalidHeaderReceived)
                {
                    Interlocked.Increment(ref this.syncingCount);
                    node.SendMessageAsync(new GetHeadersPayload()
                    {
                        BlockLocators = this.GetPendingTipOrChainTip().GetLocator()
                    });
                }
            }
        }

        private ChainedBlock GetPendingTipOrChainTip()
        {
            this.pendingTip = this.pendingTip ?? this.chainState.ConsensusTip ?? this.Chain.Tip;
            return this.pendingTip;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
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