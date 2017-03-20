using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
	// A puller that download blocks from peers
	// this must be inherited and the implementing class
	// needs to handle taking blocks off the queue and stalling
	public abstract class BlockPuller : IBlockPuller
	{
		public class DownloadedBlock
		{
			public int Length;
			public Block Block;
		}

		public class BlockPullerBehavior : NodeBehavior 
		{
			private readonly BlockPuller puller;
			private readonly CancellationTokenSource cancellationToken;
			private readonly ConcurrentDictionary<uint256, uint256> pendingDownloads;
			public BlockPullerBehavior(BlockPuller puller)
			{
				this.puller = puller;
				this.cancellationToken = new CancellationTokenSource();
				this.pendingDownloads = new ConcurrentDictionary<uint256, uint256>();
				this.QualityScore = 75;
			}
			public override object Clone()
			{
				return new BlockPullerBehavior(this.puller);
			}

			public int QualityScore
			{
				get; set;
			}

			public BlockPuller Puller => this.puller;

			public CancellationTokenSource CancellationTokenSource => this.cancellationToken;

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

				message.Message.IfPayloadIs<BlockPayload>((block) =>
				{
					block.Object.Header.CacheHashes();
					QualityScore = Math.Min(MaxQualityScore, QualityScore + 1);
					uint256 unused;
					if(this.pendingDownloads.TryRemove(block.Object.Header.GetHash(), out unused))
					{
						BlockPullerBehavior unused2;
						if (this.puller.map.TryRemove(block.Object.Header.GetHash(), out unused2))
						{
							foreach (var tx in block.Object.Transactions)
								tx.CacheHashes();
							this.puller.PushBlock((int)message.Length, block.Object, this.cancellationToken.Token);
							this.AssignPendingVector();
						}
						else
						{
							throw new InvalidOperationException("This should not happen, please notify the devs");
						}
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
				this.cancellationToken.Cancel();
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
				BlockPullerBehavior unused;
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

			//public uint256 LastUnknownBlock { get; private set; }
			//public ChainedBlock BestKnownBlock { get; private set; }

			//// Check whether the last unknown block a peer advertised is not yet known. 
			//private void ProcessBlockAvailability()
			//{
			//	if (this.LastUnknownBlock != null)
			//	{
			//		var chainedBlock = this._Puller.Chain.GetBlock(this.LastUnknownBlock);
			//		if (chainedBlock != null && chainedBlock.ChainWork > 0)
			//		{
			//			if (this.BestKnownBlock == null || chainedBlock.ChainWork >= this.BestKnownBlock.ChainWork)
			//				this.BestKnownBlock = chainedBlock;
			//			this.LastUnknownBlock = null;
			//		}
			//	}
			//}

			//// Update tracking information about which blocks a peer is assumed to have. 
			//private void UpdateBlockAvailability(uint256 hash)
			//{
			//	ProcessBlockAvailability();

			//	var chainedBlock = this._Puller.Chain.GetBlock(hash);
			//	if (chainedBlock != null && chainedBlock.ChainWork > 0)
			//	{
			//		// An actually better block was announced.
			//		if (this.BestKnownBlock == null || chainedBlock.ChainWork >= this.BestKnownBlock.ChainWork)
			//			this.BestKnownBlock = chainedBlock;
			//	}
			//	else
			//	{
			//		// An unknown block was announced; just assume that the latest one is the best one.
			//		this.LastUnknownBlock = hash;
			//	}
			//}
		}

		protected readonly NodesCollection Nodes;
		protected readonly ConcurrentChain Chain;
		private readonly NodeRequirement requirements;

		protected BlockPuller(ConcurrentChain chain, NodesCollection nodes)
		{
			this.Chain = chain;
			this.Nodes = nodes;
			this.DownloadedBlocks = new ConcurrentDictionary<uint256, DownloadedBlock>();
			this.pendingInventoryVectors = new ConcurrentBag<uint256>();
			this.map = new ConcurrentDictionary<uint256, BlockPullerBehavior>();

			// set the default requirements
			this.requirements = new NodeRequirement
			{
				MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
				RequiredServices = NodeServices.Network
			};
		}

		private readonly ConcurrentDictionary<uint256, BlockPullerBehavior> map;
		private readonly ConcurrentBag<uint256> pendingInventoryVectors;
		protected readonly ConcurrentDictionary<uint256, DownloadedBlock> DownloadedBlocks;

		/// <summary>
		/// Psuh a block using the cancellation token belonging to the behaviour that pushed the block
		/// </summary>
		public virtual void PushBlock(int length, Block block, CancellationToken token)
		{
			var hash = block.Header.GetHash();
			this.DownloadedBlocks.TryAdd(hash, new DownloadedBlock { Block = block, Length = length });
		}

		public virtual void AskBlocks(ChainedBlock[] downloadRequests)
		{
			BlockPullerBehavior[] nodes = GetNodeBehaviors();
			var vectors = downloadRequests.Select(r => new InventoryVector(InventoryType.MSG_BLOCK, r.HashBlock)).ToArray();
			DistributeDownload(vectors, nodes, downloadRequests.Min(d => d.Height));
		}

		private BlockPullerBehavior[] GetNodeBehaviors()
		{
			return Nodes
				.Where(n => requirements.Check(n.PeerVersion))
				.SelectMany(n => n.Behaviors.OfType<BlockPullerBehavior>())
				.Where(b => b.Puller == this)
				.ToArray();
		}

		private void AssignPendingVectors()
		{
			var innernodes = GetNodeBehaviors();
			if(innernodes.Length == 0)
				return;
			List<InventoryVector> vectors = new List<InventoryVector>();
			uint256 result;
			while(pendingInventoryVectors.TryTake(out result))
			{
				vectors.Add(new InventoryVector(InventoryType.MSG_BLOCK, result));
			}
			if (vectors.Any())
			{
				var minHeight = vectors.Select(v => this.Chain.GetBlock(v.Hash).Height).Min();
				DistributeDownload(vectors.ToArray(), innernodes, minHeight);
			}
		}

		public bool IsDownloading(uint256 hash)
		{
			return map.ContainsKey(hash) || pendingInventoryVectors.Contains(hash);
		}

		protected void OnStalling(ChainedBlock chainedBlock)
		{
			BlockPullerBehavior behavior = null;
			if(map.TryGetValue(chainedBlock.HashBlock, out behavior))
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

		private void DistributeDownload(InventoryVector[] vectors, BlockPullerBehavior[] innernodes, int minHight)
		{
			if(vectors.Length == 0)
				return;

			// Be careful to not ask block to a node that do not have it 
			// (we can check the ChainBehavior.PendingTip to know where the node is standing)
			var selectnodes = new List<BlockPullerBehavior>();
			foreach (var behavior in innernodes)
			{
				// filter nodes that are still behind
				if(behavior.BestKnownTip?.Height >= minHight)
					selectnodes.Add(behavior);
			}
			innernodes = selectnodes.ToArray();

			if (innernodes.Length == 0)
			{
				foreach(var v in vectors)
					pendingInventoryVectors.Add(v.Hash);
				return;
			}

			var scores = innernodes.Select(n => n.QualityScore == MaxQualityScore ? MaxQualityScore * 2 : n.QualityScore).ToArray();
			var totalScore = scores.Sum();
			GetDataPayload[] getDatas = Nodes.Select(n => new GetDataPayload()).ToArray();
			foreach(var inv in vectors)
			{
				var index = GetNodeIndex(scores, totalScore);
				var node = innernodes[index];
				var getData = getDatas[index];
				if(map.TryAdd(inv.Hash, node))
					getData.Inventory.Add(inv);
			}
			for(int i = 0; i < innernodes.Length; i++)
			{
				if(getDatas[i].Inventory.Count == 0)
					continue;
				innernodes[i].StartDownload(getDatas[i]);
			}
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

		protected virtual NodeRequirement Requirements => requirements;
	}
}
