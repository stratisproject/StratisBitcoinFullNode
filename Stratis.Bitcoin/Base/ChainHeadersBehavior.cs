#if !NOSOCKET
using System;
using System.Linq;
using System.Threading;
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
    public partial class ChainHeadersBehavior : NodeBehavior
    {
        ChainState chainState;
        public ChainHeadersBehavior(ConcurrentChain chain, ChainState chainState)
        {
            Guard.NotNull(chain, nameof(chain));

            this.chainState = chainState;
            this.chain = chain;
            this.AutoSync = true;
            this.CanSync = true;
            this.CanRespondToGetHeaders = true;
        }

        public ChainState SharedState
        {
            get
            {
                return this.chainState;
            }
        }
        /// <summary>
        /// Keep the chain in Sync (Default : true)
        /// </summary>
        public bool CanSync
        {
            get;
            set;
        }
        /// <summary>
        /// Respond to getheaders messages (Default : true)
        /// </summary>
        public bool CanRespondToGetHeaders
        {
            get;
            set;
        }

        private ConcurrentChain chain;
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

        int syncingCount;
        /// <summary>
        /// Using for test, this might not be reliable
        /// </summary>
        internal bool Syncing
        {
            get
            {
                return this.syncingCount != 0;
            }
        }

        private Timer refreshTimer;
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
                var highPoW = this.SharedState.ConsensusTip;
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
                    this.refreshTimer.Dispose(); //No need of periodical refresh, the peer is notifying us
                    if (this.AutoSync)
                        this.TrySync();
                }
            }

            // == GetHeadersPayload ==
            // represents our height from the peer's point of view 
            // it is sent from the peer on first connect, in response to  Inv(Block) 
            // or in response to HeaderPayload until an empty array is returned
            // this payload notifies peers of our current best validated height 
            // use the ChainState.HighestValidatedPoW property (not Chain.Tip)
            // if the peer is behind/equal to our best height an empty array is sent back

            // Ignoring getheaders from peers because node is in initial block download
            var getheaders = message.Message.Payload as GetHeadersPayload;
            if (getheaders != null && this.CanRespondToGetHeaders &&
                (!this.SharedState.IsInitialBlockDownload ||
                this.AttachedNode.Behavior<ConnectionManagerBehavior>().Whitelisted)) // if not in IBD whitelisted won't be checked
            {
                HeadersPayload headers = new HeadersPayload();
                var highestPow = this.SharedState.ConsensusTip;
                highestPow = this.Chain.GetBlock(highestPow.HashBlock);
                var fork = this.Chain.FindFork(getheaders.BlockLocators);

                if (fork != null)
                {
                    if (highestPow == null || fork.Height > highestPow.Height)
                    {
                        fork = null; //fork not yet validated
                    }
                    if (fork != null)
                    {
                        foreach (var header in this.Chain.EnumerateToTip(fork).Skip(1))
                        {
                            if (header.Height > highestPow.Height)
                                break;
                            headers.Headers.Add(header.Header);
                            if (header.HashBlock == getheaders.HashStop || headers.Headers.Count == 2000)
                                break;
                        }
                    }
                }
                this.AttachedNode.SendMessageAsync(headers);
            }

            // == HeadersPayload ==
            // represents the peers height from our point view
            // this updates the pending tip parameter which is the 
            // peers current best validated height
            // if the peer's height is higher Chain.Tip is updated to have 
            // the most PoW header
            // is sent in response to GetHeadersPayload or is solicited by the 
            // peer when a new block is validated (and not in IBD)

            var newheaders = message.Message.Payload as HeadersPayload;
            var pendingTipBefore = this.GetPendingTipOrChainTip();
            if (newheaders != null && this.CanSync)
            {
                // TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

                var tip = this.GetPendingTipOrChainTip();
                foreach (var header in newheaders.Headers)
                {
                    var prev = tip.FindAncestorOrSelf(header.HashPrevBlock);
                    if (prev == null)
                        break;
                    tip = new ChainedBlock(header, header.GetHash(), prev);
                    var validated = this.Chain.GetBlock(tip.HashBlock) != null || tip.Validate(this.AttachedNode.Network);
                    validated &= !this.SharedState.IsMarkedInvalid(tip.HashBlock);
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

                var chainedPendingTip = this.Chain.GetBlock(this.pendingTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    this.pendingTip = chainedPendingTip; //This allows garbage collection to collect the duplicated pendingtip and ancestors
                }

                if (newheaders.Headers.Count != 0 && pendingTipBefore.HashBlock != this.GetPendingTipOrChainTip().HashBlock)
                    this.TrySync();

                Interlocked.Decrement(ref this.syncingCount);
            }

            act();
        }

        public void SetPendingTip(ChainedBlock newTip)
        {
            if (newTip.ChainWork > this.PendingTip.ChainWork)
            {
                var chainedPendingTip = this.Chain.GetBlock(newTip.HashBlock);
                if (chainedPendingTip != null)
                {
                    this.pendingTip = chainedPendingTip;
                    //This allows garbage collection to collect the duplicated pendingtip and ancestors
                }
            }
        }

        /// <summary>
        /// Check if any past blocks announced by this peer is in the invalid blocks list, and set InvalidHeaderReceived flag accordingly
        /// </summary>
        /// <returns>True if no invalid block is received</returns>
        public bool CheckAnnouncedBlocks()
        {
            var tip = this.pendingTip;
            if (tip != null && !this.invalidHeaderReceived)
            {
                try
                {
                    this.chainState.invalidBlocksLock.EnterReadLock();
                    if (this.chainState.invalidBlocks.Count != 0)
                    {
                        foreach (var header in tip.EnumerateToGenesis())
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

        /// <summary>
        /// Sync the chain as headers come from the network (Default : true)
        /// </summary>
        public bool AutoSync
        {
            get;
            set;
        }

        private ChainedBlock pendingTip; //Might be different than Chain.Tip, in the rare event of large fork > 2000 blocks

        private bool invalidHeaderReceived;
        public bool InvalidHeaderReceived
        {
            get
            {
                return this.invalidHeaderReceived;
            }
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            this.TrySync();
        }

        /// <summary>
        /// Asynchronously try to sync the chain
        /// </summary>
        public void TrySync()
        {
            var node = this.AttachedNode;
            if (node != null)
            {
                if (node.State == NodeState.HandShaked && this.CanSync && !this.invalidHeaderReceived)
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
            this.pendingTip = this.pendingTip ?? this.SharedState.ConsensusTip ?? this.Chain.Tip;
            return this.pendingTip;
        }

        public ChainedBlock PendingTip
        {
            get
            {
                var tip = this.pendingTip;
                if (tip == null)
                    return null;
                //Prevent memory leak by returning a block from the chain instead of real pending tip of possible
                return this.Chain.GetBlock(tip.HashBlock) ?? tip;
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
        }


        #region ICloneable Members

        public override object Clone()
        {
            var clone = new ChainHeadersBehavior(this.Chain, this.SharedState)
            {
                CanSync = this.CanSync,
                CanRespondToGetHeaders = this.CanRespondToGetHeaders,
                AutoSync = this.AutoSync,
                chainState = this.chainState
            };
            return clone;
        }

        #endregion
    }
}
#endif