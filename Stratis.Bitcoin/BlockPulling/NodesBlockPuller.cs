using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using System.Collections.Concurrent;

namespace Stratis.Bitcoin.BlockPulling
{
	public class NodesBlockPuller : LookaheadBlockPuller
	{
		class Download
		{
			public Node Node
			{
				get; set;
			}
		}
		NodesCollection _Nodes;
		ConcurrentChain _Chain;
		public NodesBlockPuller(ConcurrentChain chain, NodesCollection nodes)
		{
			_Chain = chain;
			_Nodes = nodes;
			_Nodes.Added += _Nodes_Added;
			_Nodes.Removed += _Nodes_Removed;
			foreach(var node in _Nodes)
				node.MessageReceived += Node_MessageReceived;
		}

		ConcurrentDictionary<uint256, Download> _Map = new ConcurrentDictionary<uint256, Download>();
		ConcurrentBag<uint256> _PendingInventoryVectors = new ConcurrentBag<uint256>();


		private void _Nodes_Removed(object sender, NodeEventArgs e)
		{
			foreach(var download in _Map.ToArray())
			{
				if(download.Value.Node == e.Node)
				{
					Download d;
					if(_Map.TryRemove(download.Key, out d))
					{
						_PendingInventoryVectors.Add(download.Key);
					}
				}
			}
			e.Node.MessageReceived -= Node_MessageReceived;
		}

		private void _Nodes_Added(object sender, NodeEventArgs e)
		{
			e.Node.MessageReceived += Node_MessageReceived;
			AssignPendingVector(e.Node);
		}

		private void AssignPendingVector(Node node)
		{
			uint256 block;
			if(_PendingInventoryVectors.TryTake(out block))
			{
				if(_Map.TryAdd(block, new Download() { Node = node }))
					node.SendMessageAsync(new GetDataPayload(new InventoryVector(InventoryType.MSG_BLOCK, block)));
			}
		}

		private void Node_MessageReceived(Node node, IncomingMessage message)
		{
			message.Message.IfPayloadIs<BlockPayload>((block) =>
			{
				block.Object.Header.CacheHashes();
				Download v;
				_Map.TryRemove(block.Object.Header.GetHash(), out v);
				foreach(var tx in block.Object.Transactions)
					tx.CacheHashes();
				PushBlock((int)message.Length, block.Object);
				AssignPendingVector(node);
			});
		}

		protected override void AskBlocks(ChainedBlock[] downloadRequests)
		{
			var busyNodes = new HashSet<Node>(_Map.Select(m => m.Value.Node).Distinct());
			var idleNodes = _Nodes.Where(n => !busyNodes.Contains(n)).ToArray();
			if(idleNodes.Length == 0)
				idleNodes = busyNodes.ToArray();

			var vectors = downloadRequests.Select(r => new InventoryVector(InventoryType.MSG_BLOCK, r.HashBlock)).ToArray();
			if(idleNodes.Length == 0)
			{
				foreach(var v in vectors)
					_PendingInventoryVectors.Add(v.Hash);
			}
			else
			{
				DistributeDownload(vectors, idleNodes);
			}
		}

		private void DistributeDownload(InventoryVector[] vectors, Node[] idleNodes)
		{
			int nodeIndex = 0;
			//TODO: Be careful to not ask block to a node that do not have it (we can check the ChainBehavior.PendingTip to know where the node is standing)
			foreach(var batch in vectors.Partition(vectors.Length / idleNodes.Length))
			{
				var node = idleNodes[nodeIndex % idleNodes.Length];
				var getData = new GetDataPayload(batch.ToArray());
				foreach(var inv in batch)
				{
					if(!_Map.TryAdd(inv.Hash, new Download() { Node = node }))
						getData.Inventory.Remove(inv);
				}
				if(getData.Inventory.Count > 0)
				{
					node.SendMessageAsync(getData);
					nodeIndex++;
				}
			}
		}

		protected override ConcurrentChain ReloadChainCore()
		{
			return _Chain;
		}
	}
}
