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
	public class NodesBlockPuller : LookaheadBlockPuller
	{
		public class NodesBlockPullerBehavior : NodeBehavior
		{
			private readonly NodesBlockPuller _Puller;
			private CancellationTokenSource _Cts = new CancellationTokenSource();
			public NodesBlockPullerBehavior(NodesBlockPuller puller)
			{
				_Puller = puller;
			}
			public override object Clone()
			{
				return new NodesBlockPullerBehavior(_Puller);
			}

			public int QualityScore
			{
				get; set;
			} = 75;



			private ConcurrentDictionary<uint256, uint256> _PendingDownloads = new ConcurrentDictionary<uint256, uint256>();
			public ICollection<uint256> PendingDownloads
			{
				get
				{
					return _PendingDownloads.Values;
				}
			}

			private void Node_MessageReceived(Node node, IncomingMessage message)
			{
				// Attempting to find the peers current best chain
				// to be used by the puller to determine if the peer can server blocks
				message.Message.IfPayloadIs<HeadersPayload>(header =>
				{
					foreach (var blockHeader in header.Headers)
					{
						var cahinedBlock = this._Puller.Chain.GetBlock(blockHeader.GetHash());
						this.TrySetBestKnownTip(cahinedBlock);
					}
				});

				message.Message.IfPayloadIs<BlockPayload>((block) =>
				{
					block.Object.Header.CacheHashes();
					QualityScore = Math.Min(MaxQualityScore, QualityScore + 1);
					uint256 unused;
					if(!_PendingDownloads.TryRemove(block.Object.Header.GetHash(), out unused))
					{
						//TODO: Add support for unsollicited block
						// This can be sent by a miner discovering new block
						// the expected logic:
						// - partially validate the header
						// - check if block is a new best chain
						// - set Chain.Tip (and corresponding ChainBehaviour.PendingTIp for this node)
						// - push the block to the download dictionary
						return;
					}
					NodesBlockPullerBehavior unused2;
					if(_Puller._Map.TryRemove(block.Object.Header.GetHash(), out unused2))
					{
						foreach(var tx in block.Object.Transactions)
							tx.CacheHashes();
						_Puller.PushBlock((int)message.Length, block.Object, _Cts.Token);
						this.AssignPendingVector();
					}
				});
			}

			internal void AssignPendingVector()
			{
				if(AttachedNode == null || AttachedNode.State != NodeState.HandShaked || !_Puller._Requirements.Check(AttachedNode.PeerVersion))
					return;
				uint256 block;
				if(_Puller._PendingInventoryVectors.TryTake(out block))
				{
					StartDownload(block);
				}
			}

			internal void StartDownload(uint256 block)
			{
				if(_Puller._Map.TryAdd(block, this))
				{
					_PendingDownloads.TryAdd(block, block);
					AttachedNode.SendMessageAsync(new GetDataPayload(new InventoryVector(AttachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block)));
				}
			}

			//Caller should add to the puller map
			internal void StartDownload(GetDataPayload getDataPayload)
			{
				foreach(var inv in getDataPayload.Inventory)
				{
					inv.Type = AttachedNode.AddSupportedOptions(inv.Type);
					_PendingDownloads.TryAdd(inv.Hash, inv.Hash);
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
				foreach(var download in _Puller._Map.ToArray())
				{
					if(download.Value == this)
					{
						Release(download.Key);
					}
				}
			}

			internal void Release(uint256 blockHash)
			{
				NodesBlockPullerBehavior unused;
				uint256 unused2;
				if(_Puller._Map.TryRemove(blockHash, out unused))
				{
					_PendingDownloads.TryRemove(blockHash, out unused2);
					_Puller._PendingInventoryVectors.Add(blockHash);
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

		private NodesCollection _Nodes;

		public NodesBlockPuller(ConcurrentChain chain, NodesCollection nodes)
		{
			Chain = chain;
			_Nodes = nodes;
		}

		private ConcurrentDictionary<uint256, NodesBlockPullerBehavior> _Map = new ConcurrentDictionary<uint256, NodesBlockPullerBehavior>();
		private ConcurrentBag<uint256> _PendingInventoryVectors = new ConcurrentBag<uint256>();

		protected override void AskBlocks(ChainedBlock[] downloadRequests)
		{
			NodesBlockPullerBehavior[] nodes = GetNodeBehaviors();
			var vectors = downloadRequests.Select(r => new InventoryVector(InventoryType.MSG_BLOCK, r.HashBlock)).ToArray();
			DistributeDownload(vectors, nodes);
		}

		private NodesBlockPullerBehavior[] GetNodeBehaviors()
		{
			return _Nodes.Where(n => _Requirements.Check(n.PeerVersion)).Select(n => n.Behaviors.Find<NodesBlockPullerBehavior>()).ToArray();
		}

		private void AssignPendingVectors()
		{
			var nodes = GetNodeBehaviors();
			if(nodes.Length == 0)
				return;
			List<InventoryVector> vectors = new List<InventoryVector>();
			uint256 result;
			while(_PendingInventoryVectors.TryTake(out result))
			{
				vectors.Add(new InventoryVector(InventoryType.MSG_BLOCK, result));
			}
			DistributeDownload(vectors.ToArray(), nodes);
		}

		public override bool IsDownloading(uint256 hash)
		{
			return _Map.ContainsKey(hash) || _PendingInventoryVectors.Contains(hash);
		}

		protected override void OnStalling(ChainedBlock chainedBlock)
		{
			NodesBlockPullerBehavior behavior = null;
			if(_Map.TryGetValue(chainedBlock.HashBlock, out behavior))
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

		private void DistributeDownload(InventoryVector[] vectors, NodesBlockPullerBehavior[] nodes)
		{
			if(vectors.Length == 0)
				return;

			// Be careful to not ask block to a node that do not have it 
			// (we can check the ChainBehavior.PendingTip to know where the node is standing)
			List<NodesBlockPullerBehavior> selectnodes = new List<NodesBlockPullerBehavior>();
			foreach (var behavior in nodes)
			{
				// filter nodes that are still behind
				if(behavior.BestKnownTip?.Height >= this.Location.Height)
					selectnodes.Add(behavior);
			}
			nodes = selectnodes.ToArray();

			if (nodes.Length == 0)
			{
				foreach(var v in vectors)
					_PendingInventoryVectors.Add(v.Hash);
				return;
			}

			var scores = nodes.Select(n => n.QualityScore == MaxQualityScore ? MaxQualityScore * 2 : n.QualityScore).ToArray();
			var totalScore = scores.Sum();
			GetDataPayload[] getDatas = nodes.Select(n => new GetDataPayload()).ToArray();
			foreach(var inv in vectors)
			{
				var index = GetNodeIndex(scores, totalScore);
				var node = nodes[index];
				var getData = getDatas[index];
				if(_Map.TryAdd(inv.Hash, node))
					getData.Inventory.Add(inv);
			}
			for(int i = 0; i < nodes.Length; i++)
			{
				if(getDatas[i].Inventory.Count == 0)
					continue;
				nodes[i].StartDownload(getDatas[i]);
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

		private NodeRequirement _Requirements = new NodeRequirement
		{
			MinVersion = ProtocolVersion.SENDHEADERS_VERSION,
			RequiredServices = NodeServices.Network
		};
		public override void RequestOptions(TransactionOptions transactionOptions)
		{
			if(transactionOptions == TransactionOptions.Witness)
			{
				_Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
				foreach(var node in _Nodes.Select(n => n.Behaviors.Find<NodesBlockPullerBehavior>()))
				{
					if(!_Requirements.Check(node.AttachedNode.PeerVersion))
					{
						node.ReleaseAll();
					}
				}
			}
		}
	}
}
