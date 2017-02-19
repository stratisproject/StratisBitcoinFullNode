#if !NOSOCKET
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;

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
			if (chain == null)
				throw new ArgumentNullException("chain");
			_State = chainState;
			_Chain = chain;
			AutoSync = true;
			CanSync = true;
			CanRespondToGetHeaders = true;
		}

		public ChainState SharedState
		{
			get
			{
				return _State;
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
				return _Chain;
			}
			set
			{
				AssertNotAttached();
				_Chain = value;
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
				return _SynchingCount != 0;
			}
		}

		Timer _Refresh;
		protected override void AttachCore()
		{
			_Refresh = new Timer(o =>
			{
				if (AutoSync)
					TrySync();
			}, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);
			RegisterDisposable(_Refresh);
			if (AttachedNode.State == NodeState.Connected)
			{
				var highPoW = SharedState.HighestValidatedPoW;
				AttachedNode.MyVersion.StartHeight = highPoW.Height;
			}
			AttachedNode.StateChanged += AttachedNode_StateChanged;
			RegisterDisposable(AttachedNode.Filters.Add(Intercept));
		}

		void Intercept(IncomingMessage message, Action act)
		{
			var inv = message.Message.Payload as InvPayload;
			if (inv != null)
			{
				if (inv.Inventory.Any(i => ((i.Type & InventoryType.MSG_BLOCK) != 0) && !Chain.Contains(i.Hash)))
				{
					_Refresh.Dispose(); //No need of periodical refresh, the peer is notifying us
					if (AutoSync)
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
			if (getheaders != null && CanRespondToGetHeaders &&
			    (!this.SharedState.IsInitialBlockDownload || 
				this.AttachedNode.Behavior<ConnectionManagerBehavior>().Whitelisted)) // if not in IBD whitelisted won't be checked
			{
				HeadersPayload headers = new HeadersPayload();
				var highestPow = SharedState.HighestValidatedPoW;
				highestPow = Chain.GetBlock(highestPow.HashBlock);
				var fork = Chain.FindFork(getheaders.BlockLocators);

				if (fork != null)
				{
					// this is the only indicator to know the
					// height of peers that are behind
					this.LastFork = fork;

					if (fork.Height > highestPow.Height)
					{
						fork = null; //fork not yet validated
					}
					if (fork != null)
					{
						foreach (var header in Chain.EnumerateToTip(fork).Skip(1))
						{
							if (header.Height > highestPow.Height)
								break;
							headers.Headers.Add(header.Header);
							if (header.HashBlock == getheaders.HashStop || headers.Headers.Count == 2000)
								break;
						}
					}
				}
				AttachedNode.SendMessageAsync(headers);
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
			if (newheaders != null && CanSync)
			{
				// TODO: implement MAX_HEADERS_RESULTS in NBitcoin.HeadersPayload

				var tip = GetPendingTipOrChainTip();
				foreach (var header in newheaders.Headers)
				{
					var prev = tip.FindAncestorOrSelf(header.HashPrevBlock);
					if (prev == null)
						break;
					tip = new ChainedBlock(header, header.GetHash(), prev);
					var validated = Chain.GetBlock(tip.HashBlock) != null || tip.Validate(AttachedNode.Network);
					validated &= !SharedState.IsMarkedInvalid(tip.HashBlock);
					if (!validated)
					{
						invalidHeaderReceived = true;
						break;
					}
					_PendingTip = tip;
				}

				if (_PendingTip.ChainWork > Chain.Tip.ChainWork)
				{
					Chain.SetTip(_PendingTip);
				}

				var chainedPendingTip = Chain.GetBlock(_PendingTip.HashBlock);
				if (chainedPendingTip != null)
				{
					_PendingTip = chainedPendingTip; //This allows garbage collection to collect the duplicated pendingtip and ancestors
				}

				if (newheaders.Headers.Count != 0 && pendingTipBefore.HashBlock != GetPendingTipOrChainTip().HashBlock)
					TrySync();

				Interlocked.Decrement(ref _SynchingCount);
			}

			act();
		}

		/// <summary>
		/// Check if any past blocks announced by this peer is in the invalid blocks list, and set InvalidHeaderReceived flag accordingly
		/// </summary>
		/// <returns>True if no invalid block is received</returns>
		public bool CheckAnnouncedBlocks()
		{
			var tip = _PendingTip;
			if (tip != null && !invalidHeaderReceived)
			{
				try
				{
					_State._InvalidBlocksLock.EnterReadLock();
					if (_State._InvalidBlocks.Count != 0)
					{
						foreach (var header in tip.EnumerateToGenesis())
						{
							if (invalidHeaderReceived)
								break;
							invalidHeaderReceived |= _State._InvalidBlocks.Contains(header.HashBlock);
						}
					}
				}
				finally
				{
					_State._InvalidBlocksLock.ExitReadLock();
				}
			}
			return !invalidHeaderReceived;
		}

		public ChainedBlock LastFork { get; private set; }

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
				return invalidHeaderReceived;
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
			var node = AttachedNode;
			if (node != null)
			{
				if (node.State == NodeState.HandShaked && CanSync && !invalidHeaderReceived)
				{
					Interlocked.Increment(ref _SynchingCount);
					node.SendMessageAsync(new GetHeadersPayload()
					{
						BlockLocators = GetPendingTipOrChainTip().GetLocator()
					});
				}
			}
		}

		private ChainedBlock GetPendingTipOrChainTip()
		{
			_PendingTip = _PendingTip ?? this.SharedState.HighestValidatedPoW;// Chain.Tip;
			return _PendingTip;
		}

		public ChainedBlock PendingTip
		{
			get
			{
				var tip = _PendingTip;
				if (tip == null)
					return null;
				//Prevent memory leak by returning a block from the chain instead of real pending tip of possible
				return Chain.GetBlock(tip.HashBlock) ?? tip;
			}
		}

		protected override void DetachCore()
		{
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
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
					_InvalidBlocksLock.EnterReadLock();
					return _InvalidBlocks.Contains(hashBlock);
				}
				finally
				{
					_InvalidBlocksLock.ExitReadLock();
				}
			}

			public void MarkBlockInvalid(uint256 blockHash)
			{
				try
				{
					_InvalidBlocksLock.EnterWriteLock();
					_InvalidBlocks.Add(blockHash);
				}
				finally
				{
					_InvalidBlocksLock.ExitWriteLock();
				}
			}

			private long lastupdate;
			private bool lastresult;
			public bool IsInitialBlockDownload
			{
				get
				{
					if (lastupdate < this.fullNode.DateTimeProvider.GetUtcNow().Ticks)
					{
						lastupdate = this.fullNode.DateTimeProvider.GetUtcNow().AddMinutes(1).Ticks; // sample every minute
						lastresult = this.fullNode.IsInitialBlockDownload();
					}
					return lastresult;
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
		}

		#region ICloneable Members

		public override object Clone()
		{
			var clone = new BlockStore.ChainBehavior(Chain, this.SharedState)
			{
				CanSync = CanSync,
				CanRespondToGetHeaders = CanRespondToGetHeaders,
				AutoSync = AutoSync,
				_State = _State
			};
			return clone;
		}

		#endregion
	}
}
#endif