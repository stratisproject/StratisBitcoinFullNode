using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.BlockPulling
{
	public interface ILookaheadBlockPuller
	{
		Block TryGetLookahead(int count);

		void SetLocation(ChainedBlock location);

		Block NextBlock(CancellationToken cancellationToken);

		void RequestOptions(TransactionOptions transactionOptions);
	}
	public class LookaheadBlockPuller : BlockPuller, ILookaheadBlockPuller
	{

		private const int BLOCK_SIZE = 2000000;
		public LookaheadBlockPuller(ConcurrentChain chain, IConnectionManager connectionManager) 
			: base(chain, connectionManager.ConnectedNodes, connectionManager.NodeSettings.ProtocolVersion)
		{
			this.MaxBufferedSize = BLOCK_SIZE * 10;
			this.MinimumLookahead = 4;
            this.MaximumLookahead = 2000;
		}

		public int MinimumLookahead
		{
			get;
			set;
		}

		public int MaximumLookahead
		{
			get;
			set;
		}

		private int _ActualLookahead;
		public int ActualLookahead
		{
			get
			{
				return Math.Min(this.MaximumLookahead, Math.Max(this.MinimumLookahead, this._ActualLookahead));
			}
			private set
			{
                this._ActualLookahead = Math.Min(this.MaximumLookahead, Math.Max(this.MinimumLookahead, value));
			}
		}

		public int DownloadedCount
		{
			get { return this.DownloadedBlocks.Count; }
		}

		public ChainedBlock Location
		{
			get { return this._Location; }
		}

		private int _CurrentDownloading = 0;

		public int MaxBufferedSize
		{
			get;
			set;
		}

		public bool Stalling
		{
			get;
			internal set;
		}

		private long _CurrentSize;

		private ChainedBlock _Location;
		private ChainedBlock _LookaheadLocation;
		public ChainedBlock LookaheadLocation
		{
			get
			{
				return this._LookaheadLocation;
			}
		}

		public void SetLocation(ChainedBlock tip)
		{
			Guard.NotNull(tip, nameof(tip));
            this._Location = tip;
		}

		public void RequestOptions(TransactionOptions transactionOptions)
		{
			if (transactionOptions == TransactionOptions.Witness)
			{
				this.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
				foreach (var node in this.Nodes.Select(n => n.Behaviors.Find<BlockPullerBehavior>()))
				{
					if (!this.Requirements.Check(node.AttachedNode.PeerVersion))
					{
						node.ReleaseAll();
					}
				}
			}
		}

		public Block NextBlock(CancellationToken cancellationToken)
		{
            this._DownloadedCounts.Add(this.DownloadedBlocks.Count);
			if(this._LookaheadLocation == null)
			{
				AskBlocks();
				AskBlocks();
			}
			var block = NextBlockCore(cancellationToken);
			if(block == null)
			{
				//A reorg
				return null;
			}
			if((this._LookaheadLocation.Height - this._Location.Height) <= this.ActualLookahead)
			{
				CalculateLookahead();
				AskBlocks();
			}
			return block;
		}

		private static decimal GetMedian(List<int> sourceNumbers)
		{
			//Framework 2.0 version of this method. there is an easier way in F4
			if(sourceNumbers == null || sourceNumbers.Count == 0)
				throw new Exception("Median of empty array not defined.");

			//make sure the list is sorted, but use a new array
			sourceNumbers.Sort();

			//get the median
			int size = sourceNumbers.Count;
			int mid = size / 2;
			decimal median = (size % 2 != 0) ? (decimal)sourceNumbers[mid] : ((decimal)sourceNumbers[mid] + (decimal)sourceNumbers[mid - 1]) / 2;
			return median;
		}

		private List<int> _DownloadedCounts = new List<int>();
		// If blocks ActualLookahead is 8:
		// If the number of downloaded block reach 2 or below, then ActualLookahead will be multiplied by 1.1.
		// If it reach 14 or above, it will be divided by 1.1.
		private void CalculateLookahead()
		{
			var medianDownloads = (decimal)GetMedian(this._DownloadedCounts);
            this._DownloadedCounts.Clear();
			var expectedDownload = this.ActualLookahead * 1.1m;
			decimal tolerance = 0.05m;
			var margin = expectedDownload * tolerance;
			if(medianDownloads <= expectedDownload - margin)
                this.ActualLookahead = (int)Math.Max(this.ActualLookahead * 1.1m, this.ActualLookahead + 1);
			else if(medianDownloads >= expectedDownload + margin)
                this.ActualLookahead = (int)Math.Min(this.ActualLookahead / 1.1m, this.ActualLookahead - 1);
		}

		public decimal MedianDownloadCount
		{
			get
			{
				if(this._DownloadedCounts.Count == 0)
					return decimal.One;
				return GetMedian(this._DownloadedCounts);
			}
		}

		public Block TryGetLookahead(int count)
		{
			var chainedBlock = this.Chain.GetBlock(this._Location.Height + 1 + count);
			if(chainedBlock == null)
				return null;
			var block = this.DownloadedBlocks.TryGet(chainedBlock.HashBlock);
			if(block == null)
				return null;
			return block.Block;
		}

		private AutoResetEvent _Consumed = new AutoResetEvent(false);
		private AutoResetEvent _Pushed = new AutoResetEvent(false);

		/// <summary>
		/// If true, the puller is a bottleneck
		/// </summary>
		public bool IsStalling
		{
			get;
			internal set;
		}

		/// <summary>
		/// If true, the puller consumer is a bottleneck
		/// </summary>
		public bool IsFull
		{
			get;
			internal set;
		}

		// making this method public allows to push blocks directly
		// to the downloader, used for testing and mining.
		public override void PushBlock(int length, Block block, CancellationToken token)
		{
			var hash = block.Header.GetHash();
			var header = this.Chain.GetBlock(hash);
			while(this._CurrentSize + length >= this.MaxBufferedSize && header.Height != this._Location.Height + 1)
			{
				this.IsFull = true;
                this._Consumed.WaitOne(1000);
				token.ThrowIfCancellationRequested();
			}
            this.IsFull = false;
			this.DownloadedBlocks.TryAdd(hash, new DownloadedBlock { Block = block, Length = length });
            this._CurrentSize += length;
            this._Pushed.Set();
		}

		private void AskBlocks()
		{
			if(this._Location == null)
				throw new InvalidOperationException("SetLocation should have been called");
			if(this._LookaheadLocation == null && !this.Chain.Contains(this._Location))
				return;
			if(this._LookaheadLocation != null && !this.Chain.Contains(this._LookaheadLocation))
                this._LookaheadLocation = null;

			ChainedBlock[] downloadRequests = null;

			ChainedBlock lookaheadBlock = this._LookaheadLocation ?? this._Location;
			ChainedBlock nextLookaheadBlock = this.Chain.GetBlock(Math.Min(lookaheadBlock.Height + this.ActualLookahead, this.Chain.Height));
			if(nextLookaheadBlock == null)
				return;
			var fork = nextLookaheadBlock.FindFork(lookaheadBlock);

            this._LookaheadLocation = nextLookaheadBlock;

			downloadRequests = new ChainedBlock[nextLookaheadBlock.Height - fork.Height];
			if(downloadRequests.Length == 0)
				return;
			for(int i = 0; i < downloadRequests.Length; i++)
			{
				downloadRequests[downloadRequests.Length - i - 1] = nextLookaheadBlock;
				nextLookaheadBlock = nextLookaheadBlock.Previous;
			}

			AskBlocks(downloadRequests);
		}

		private static int[] waitTime = new[] { 1, 10, 20, 40, 100, 1000 };
		private Block NextBlockCore(CancellationToken cancellationToken)
		{
			int i = 0;
			while(true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var header = this.Chain.GetBlock(this._Location.Height + 1);
				DownloadedBlock block;
				if(header != null && this.DownloadedBlocks.TryRemove(header.HashBlock, out block))
				{
					if(header.Previous.HashBlock != this._Location.HashBlock)
					{
						//A reorg
						return null;
					}
                    this.IsStalling = false;
                    this._Location = header;
					Interlocked.Add(ref this._CurrentSize, -block.Length);
                    this._Consumed.Set();
					return block.Block;
				}
				else
				{
					if(header == null)
					{
						if(!this.Chain.Contains(this._Location.HashBlock))
						{
							//A reorg
							return null;
						}
					}
					else
					{
						if(!IsDownloading(header.HashBlock))
							AskBlocks(new ChainedBlock[] { header });
						OnStalling(header);
                        this.IsStalling = true;
					}
					WaitHandle.WaitAny(new[] { this._Pushed, cancellationToken.WaitHandle }, waitTime[i]);
				}
				i = i == waitTime.Length - 1 ? 0 : i + 1;
			}
		}
	}
}
