using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.FullNode.BlockPulling
{
	public class NodeBlockPuller : LookaheadBlockPuller
	{
		Node _Node;
		public NodeBlockPuller(Node node)
		{
			_Node = node;
			_Node.MessageReceived += _Node_MessageReceived;
			Lookahead = 500;
		}

		private void _Node_MessageReceived(Node node, IncomingMessage message)
		{
			message.Message.IfPayloadIs<BlockPayload>((block) =>
			{
				PushBlock((int)message.Length, block.Object);
			});			
		}

		protected override void AskBlocks(ChainedBlock[] downloadRequests)
		{
			_Node.SendMessageAsync(new GetDataPayload(downloadRequests.Select(r => new InventoryVector(InventoryType.MSG_BLOCK, r.HashBlock)).ToArray()));
		}

		protected override ConcurrentChain ReloadChainCore()
		{
			return _Node.GetChain();
		}
	}
}
