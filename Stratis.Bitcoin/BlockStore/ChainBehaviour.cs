#if !NOSOCKET
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockStore
{

	/// <summary>
	/// The Chain Behavior is responsible for keeping a ConcurrentChain up to date with the peer, it also responds to getheaders messages.
	/// </summary>
	public class ChainBehavior : NodeBehavior
	{
		ChainState _State;
		public ChainBehavior(ConcurrentChain chain, ChainState chainState)
		{
			Guard.NotNull(chain, nameof(chain));

            this._State = chainState;
			this._Chain = chain;
            this.AutoSync = true;
            this.CanSync = true;
            this.CanRespondToGetHeaders = true;
		}

		public ChainState SharedState
		{
			get
			{
				return this._State;
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

		ConcurrentChain _Chain;
		public ConcurrentChain Chain
		{
			get
			{
				return this._Chain;
			}
			set
			{
				AssertNotAttached();
                this._Chain = value;
			}
		}

		int _SynchingCount;
		/// <summary>
		/// Using for test, this might not be reliable
		/// </summary>
		internal bool Synching
		{
			get
			{
				return this._SynchingCount != 0;
			}
		}

		Timer _Refresh;
		protected override void AttachCore()
		{
            this._Refresh = new Timer(o =>
			{
				if (this.AutoSync)
					TrySync();
			}, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);
			RegisterDisposable(this._Refresh);
			if (this.AttachedNode.State == NodeState.Connected)
			{
				var highPoW = this.SharedState.HighestValidatedPoW;
                this.AttachedNode.MyVersion.StartHeight = highPoW?.Height ?? 0;
			}
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
			RegisterDisposable(this.AttachedNode.Filters.Add(Intercept));
		}

		void Intercept(IncomingMessage message, Action act)
		{
			var inv = message.Message.Payload as InvPayload;
			if (inv != null)
			{
				if (inv.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !this.Chain.Contains(i.Hash)))
				{
                    this._Refresh.Dispose(); //No need of periodical refresh, the peer is notifying us
					if (this.AutoSync)
						TrySync();
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
				var highestPow = this.SharedState.HighestValidatedPoW;
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
			var pendingTipBefore = GetPendingTipOrChainTip();
			if (newheaders != null && this.CanSync)
			{
				// TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

				var tip = GetPendingTipOrChainTip();
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
                    this._PendingTip = tip;
				}

				if (this._PendingTip.ChainWork > this.Chain.Tip.ChainWork)
				{
                    this.Chain.SetTip(this._PendingTip);
				}

				var chainedPendingTip = this.Chain.GetBlock(this._PendingTip.HashBlock);
				if (chainedPendingTip != null)
				{
                    this._PendingTip = chainedPendingTip; //This allows garbage collection to collect the duplicated pendingtip and ancestors
				}

				if (newheaders.Headers.Count != 0 && pendingTipBefore.HashBlock != GetPendingTipOrChainTip().HashBlock)
					TrySync();

				Interlocked.Decrement(ref this._SynchingCount);
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
                    this._PendingTip = chainedPendingTip;
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
			var tip = this._PendingTip;
			if (tip != null && !this.invalidHeaderReceived)
			{
				try
				{
                    this._State._InvalidBlocksLock.EnterReadLock();
					if (this._State._InvalidBlocks.Count != 0)
					{
						foreach (var header in tip.EnumerateToGenesis())
						{
							if (this.invalidHeaderReceived)
								break;
                            this.invalidHeaderReceived |= this._State._InvalidBlocks.Contains(header.HashBlock);
						}
					}
				}
				finally
				{
                    this._State._InvalidBlocksLock.ExitReadLock();
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

		ChainedBlock _PendingTip; //Might be different than Chain.Tip, in the rare event of large fork > 2000 blocks

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
			TrySync();
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
					Interlocked.Increment(ref this._SynchingCount);
					node.SendMessageAsync(new GetHeadersPayload()
					{
						BlockLocators = GetPendingTipOrChainTip().GetLocator()
					});
				}
			}
		}

		private ChainedBlock GetPendingTipOrChainTip()
		{
            this._PendingTip = this._PendingTip ?? this.SharedState.HighestValidatedPoW ?? this.Chain.Tip;
			return this._PendingTip;
		}

		public ChainedBlock PendingTip
		{
			get
			{
				var tip = this._PendingTip;
				if (tip == null)
					return null;
				//Prevent memory leak by returning a block from the chain instead of real pending tip of possible
				return this.Chain.GetBlock(tip.HashBlock) ?? tip;
			}
		}

		protected override void DetachCore()
		{
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
		}


		public class ChainState
		{
			private readonly FullNode fullNode;

			public ChainState(FullNode fullNode)
			{
				this.fullNode = fullNode;
			}

			internal ReaderWriterLockSlim _InvalidBlocksLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			internal HashSet<uint256> _InvalidBlocks = new HashSet<uint256>();

			public bool IsMarkedInvalid(uint256 hashBlock)
			{
				try
				{
                    this._InvalidBlocksLock.EnterReadLock();
					return this._InvalidBlocks.Contains(hashBlock);
				}
				finally
				{
                    this._InvalidBlocksLock.ExitReadLock();
				}
			}

			public void MarkBlockInvalid(uint256 blockHash)
			{
				try
				{
                    this._InvalidBlocksLock.EnterWriteLock();
					this._InvalidBlocks.Add(blockHash);
				}
				finally
				{
                    this._InvalidBlocksLock.ExitWriteLock();
				}
			}

			private long lastupdate;
			private bool lastresult;
			public bool IsInitialBlockDownload
			{
				get
				{
					if (this.lastupdate < this.fullNode.DateTimeProvider.GetUtcNow().Ticks)
					{
                        this.lastupdate = this.fullNode.DateTimeProvider.GetUtcNow().AddMinutes(1).Ticks; // sample every minute
                        this.lastresult = this.fullNode.IsInitialBlockDownload();
					}
					return this.lastresult;
				}
			}

			// for testing to be able to move the IBD
			public void SetIsInitialBlockDownload(bool val, DateTime time)
			{
				this.lastupdate = time.Ticks;
				this.lastresult = val;
			}

			/// <summary>
			/// ChainBehaviors sharing this state will not broadcast headers which are above HighestValidatedPoW
			/// </summary>
			public ChainedBlock HighestValidatedPoW
			{
				get; set;
			}

			/// <summary>
			/// Represents the last block stored to disk
			/// </summary>
			public ChainedBlock HighestPersistedBlock
			{
				get; set;
			}

		}

		#region ICloneable Members

		public override object Clone()
		{
			var clone = new BlockStore.ChainBehavior(this.Chain, this.SharedState)
			{
				CanSync = this.CanSync,
				CanRespondToGetHeaders = this.CanRespondToGetHeaders,
				AutoSync = this.AutoSync,
				_State = this._State
			};
			return clone;
		}

		#endregion
	}
}
#endif