using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreBehaviour : NodeBehavior
	{
		private readonly BlockStore blockStore;

		public bool CanRespondToGetBlocksPayload { get; set; }

		public bool CanRespondeToGetDataPayload { get; set; }

		public BlockStoreBehaviour(BlockStore blockStore)
		{
			this.blockStore = blockStore;
			
			this.CanRespondToGetBlocksPayload = true;
			this.CanRespondeToGetDataPayload = true;
		}

		protected override void AttachCore()
		{
			this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
		}

		protected override void DetachCore()
		{
			this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
		}

		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if (this.CanRespondeToGetDataPayload)
				message.Message.IfPayloadIs<GetDataPayload>(data => this.blockStore.ProcessGetData(node, data));

			if (this.CanRespondToGetBlocksPayload)
				message.Message.IfPayloadIs<GetBlocksPayload>(data => this.blockStore.ProcessGetBlocks(node, data));
		}

		public override object Clone()
		{
			return new BlockStoreBehaviour(this.blockStore)
			{
				CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
				CanRespondeToGetDataPayload = this.CanRespondeToGetDataPayload
			};
		}
	}
}
