using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling
{
	/// <summary>
	/// A class that takes the NodesBlockPuller without the distribution of downloads
	/// Intended to be used for catch-up scenarios for the BlockStore 
	/// When downloading is intended to block the caller till download is complete
	/// </summary>
	public class BlockingPuller
	{
		public class BlockingPullerBehavior : NodeBehavior
		{
			private readonly BlockingPuller puller;
			private CancellationTokenSource _Cts = new CancellationTokenSource();

			public BlockingPullerBehavior(BlockingPuller puller)
			{
				this.puller = puller;
			}
			public override object Clone()
			{
				return new BlockingPullerBehavior(puller);
			}

			public int QualityScore
			{
				get; set;
			} = 75;

			private readonly ConcurrentDictionary<uint256, uint256> pendingDownloads = new ConcurrentDictionary<uint256, uint256>();
			public ICollection<uint256> PendingDownloads => pendingDownloads.Values;

			private void Node_MessageReceived(Node node, IncomingMessage message)
			{
				// Attempting to find the peers current best chain
				// to be used by the puller to determine if the peer can server blocks
				message.Message.IfPayloadIs<HeadersPayload>(header =>
				{
					foreach (var blockHeader in header.Headers)
					{
						var cahinedBlock = this.puller.Chain.GetBlock(blockHeader.GetHash());
						this.TrySetBestKnownTip(cahinedBlock);
					}
				});

				message.Message.IfPayloadIs<BlockPayload>(block =>
				{
					block.Object.Header.CacheHashes();
					QualityScore = Math.Min(MaxQualityScore, QualityScore + 1);
					uint256 unused;
					if(!this.pendingDownloads.TryRemove(block.Object.Header.GetHash(), out unused))
					{
						// not for this behaviour
						return;
					}
					BlockingPullerBehavior unused2;
					if(this.puller.map.TryRemove(block.Object.Header.GetHash(), out unused2))
					{
						foreach(var tx in block.Object.Transactions)
							tx.CacheHashes();
						this.puller.PushBlock((int)message.Length, block.Object, _Cts.Token);
						this.AssignPendingVector();
					}
				});
			}

			internal void AssignPendingVector()
			{
				if(AttachedNode == null || AttachedNode.State != NodeState.HandShaked || !this.puller.requirements.Check(AttachedNode.PeerVersion))
					return;
				uint256 block;
				if(this.puller.pendingInventoryVectors.TryTake(out block))
				{
					StartDownload(block);
				}
			}

			internal void StartDownload(uint256 block)
			{
				if(this.puller.map.TryAdd(block, this))
				{
					this.pendingDownloads.TryAdd(block, block);
					AttachedNode.SendMessageAsync(new GetDataPayload(new InventoryVector(AttachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block)));
				}
			}

			//Caller should add to the puller map
			internal void StartDownload(GetDataPayload getDataPayload)
			{
				foreach(var inv in getDataPayload.Inventory)
				{
					inv.Type = AttachedNode.AddSupportedOptions(inv.Type);
					this.pendingDownloads.TryAdd(inv.Hash, inv.Hash);
				}
				AttachedNode.SendMessageAsync(getDataPayload);
			}

			protected override void AttachCore()
			{
				AttachedNode.MessageReceived += Node_MessageReceived;
				AssignPendingVector();
			}

			protected override void DetachCore()
			{
				_Cts.Cancel();
				AttachedNode.MessageReceived -= Node_MessageReceived;
				foreach(var download in this.puller.map.ToArray())
				{
					if(download.Value == this)
					{
						Release(download.Key);
					}
				}
			}

			internal void Release(uint256 blockHash)
			{
				BlockingPullerBehavior unused;
				uint256 unused2;
				if(this.puller.map.TryRemove(blockHash, out unused))
				{
					this.pendingDownloads.TryRemove(blockHash, out unused2);
					this.puller.pendingInventoryVectors.Add(blockHash);
				}
			}

			public void ReleaseAll()
			{
				foreach(var h in PendingDownloads.ToArray())
				{
					Release(h);
				}
			}

			public ChainedBlock BestKnownTip { get; private set; }

			private void TrySetBestKnownTip(ChainedBlock block)
			{
				// best know tip is only set when the headers is at the same
				// height as us or ahead, its an indicator if the node can be used
				// to download blocks from, nodes which are behind do not send headers
				if (block != null && block.ChainWork > 0)
					if (this.BestKnownTip == null || block.ChainWork > this.BestKnownTip.ChainWork)
						this.BestKnownTip = block;
			}
		}

		private NodesCollection nodes;
		public ConcurrentChain Chain
		{
			get;
			set;
		}

		public BlockingPuller(ConcurrentChain chain, NodesCollection nodes)
		{
			this.Chain = chain;
			this.nodes = nodes;
		}

		private readonly ConcurrentDictionary<uint256, BlockingPullerBehavior> map = new ConcurrentDictionary<uint256, BlockingPullerBehavior>();
		private readonly ConcurrentBag<uint256> pendingInventoryVectors = new ConcurrentBag<uint256>();

		private int running = 0;

		/// <summary>
		/// This method is blocking until all asked blocks are downloaded.
		/// Should be called only once till complete.
		/// </summary>
		public async Task<List<Block>> AskBlocks(CancellationToken token, ChainedBlock[] downloadRequests)
		{
			// no one should enter if already running
			Check.Assert(Interlocked.Increment(ref running) == 1);

			BlockingPullerBehavior[] nodes = GetNodeBehaviors();
			var vectors = downloadRequests.Select(r => new InventoryVector(InventoryType.MSG_BLOCK, r.HashBlock)).ToArray();

			// find the best block
			var best = downloadRequests.OrderByDescending(p => p.Height).First(); //sort by height

			// Be careful to not ask block to a node that do not have it 
			// (we can check the ChainBehavior.PendingTip to know where the node is standing)
			List<BlockingPullerBehavior> selectnodes = new List<BlockingPullerBehavior>();
			foreach (var behavior in nodes)
			{
				// filter nodes that are still behind
				if (behavior.BestKnownTip?.Height >= best.Height)
					selectnodes.Add(behavior);
			}

			nodes = selectnodes.ToArray();
			StartDownload(vectors, nodes);
			await Task.Delay(100, token);

			while (!token.IsCancellationRequested)
			{
				// check if blocks have arrived
				var pending = downloadRequests.Where(d => !this.downloadedBlocks.ContainsKey(d.HashBlock)).ToList();

				if (!pending.Any())
					break;
					
				var sorted = pending.OrderBy(p => p.Height).FirstOrDefault(); //sort by height

				// call to stalling blocks
				this.OnStalling(sorted); // use the earliest block for stalling
				await Task.Delay(100, token);
			}

			var downloaded = this.downloadedBlocks.Values.ToList();
			this.downloadedBlocks.Clear();
			running = 0;
			return downloaded;
		}

		private BlockingPullerBehavior[] GetNodeBehaviors()
		{
			return nodes.Where(n => this.requirements.Check(n.PeerVersion)).Select(n => n.Behaviors.Find<BlockingPullerBehavior>()).ToArray();
		}

		private void AssignPendingVectors()
		{
			var nodes = GetNodeBehaviors();
			if(nodes.Length == 0)
				return;
			List<InventoryVector> vectors = new List<InventoryVector>();
			uint256 result;
			while(this.pendingInventoryVectors.TryTake(out result))
			{
				vectors.Add(new InventoryVector(InventoryType.MSG_BLOCK, result));
			}
			StartDownload(vectors.ToArray(), nodes);
		}

		public bool IsDownloading(uint256 hash)
		{
			return this.map.ContainsKey(hash) || this.pendingInventoryVectors.Contains(hash);
		}

		protected void OnStalling(ChainedBlock chainedBlock)
		{
			BlockingPullerBehavior behavior = null;
			if(this.map.TryGetValue(chainedBlock.HashBlock, out behavior))
			{
				behavior.QualityScore = Math.Max(MinQualityScore, behavior.QualityScore - 1);
				if(behavior.QualityScore == MinQualityScore)
				{
					// TODO: this does not necessarily mean the node is slow
					// the best way is to check the nodes download speed, how
					// many kb/s the node for the node download speed.
					behavior.ReleaseAll();
					AssignPendingVectors();
				}
			}
			else
			{
				AssignPendingVectors();
			}
		}

		private readonly ConcurrentDictionary<uint256, Block> downloadedBlocks = new ConcurrentDictionary<uint256, Block>();
		public ICollection<Block> DownloadedBlocks => this.downloadedBlocks.Values;

		private void PushBlock(int length, Block block, CancellationToken cancellation)
		{
			var hash = block.Header.GetHash();
			var header = Chain.GetBlock(hash);
			this.downloadedBlocks.TryAdd(hash, block);
		}

		private void StartDownload(InventoryVector[] vectors, BlockingPullerBehavior[] nodes)
		{
			if(vectors.Length == 0)
				return;

			if (nodes.Length == 0)
			{
				foreach(var v in vectors)
					this.pendingInventoryVectors.Add(v.Hash);
				return;
			}

			// find the node with the height score
			var selected = nodes.OrderByDescending(n => n.QualityScore).First();
			var getdata = new GetDataPayload();
			foreach(var inv in vectors)
			{
				if(this.map.TryAdd(inv.Hash, selected))
					getdata.Inventory.Add(inv);
			}
			selected.StartDownload(getdata);
		}

		private const int MaxQualityScore = 150;
		private const int MinQualityScore = 1;
		private Random _Rand = new Random();
		//Chose random index proportional to the score
		private int GetNodeIndex(int[] scores, int totalScore)
		{
			var v = _Rand.Next(totalScore);
			var current = 0;
			int i = 0;
			foreach(var score in scores)
			{
				current += score;
				if(v < current)
					return i;
				i++;
			}
			return scores.Length - 1;
		}

		private readonly NodeRequirement requirements = new NodeRequirement
		{
			MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
			RequiredServices = NodeServices.Network
		};

		public void RequestOptions(TransactionOptions transactionOptions)
		{
			if(transactionOptions == TransactionOptions.Witness)
			{
				requirements.RequiredServices |= NodeServices.NODE_WITNESS;
				foreach(var node in this.nodes.Select(n => n.Behaviors.Find<BlockingPullerBehavior>()))
				{
					if(!requirements.Check(node.AttachedNode.PeerVersion))
					{
						node.ReleaseAll();
					}
				}
			}
		}
	}
}
