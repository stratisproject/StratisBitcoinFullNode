using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreBehaviour : NodeBehavior
	{
		private readonly ConcurrentChain concurrentChain;
		private readonly BlockRepository blockRepository;

		public bool CanRespondToGetBlocksPayload { get; set; }

		public bool CanRespondeToGetDataPayload { get; set; }

		public BlockStoreBehaviour(ConcurrentChain concurrentChain, BlockRepository blockRepository)
		{
			this.concurrentChain = concurrentChain;
			this.blockRepository = blockRepository;

			this.CanRespondToGetBlocksPayload = false;
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

		private async void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			await this.AttachedNode_MessageReceivedAsync(node, message);
		}

		private async Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			if (this.CanRespondeToGetDataPayload)
				await message.Message.IfPayloadIsAsync<GetDataPayload>(async data => await this.ProcessGetDataAsync(node, data));

			if (this.CanRespondToGetBlocksPayload)
				await message.Message.IfPayloadIsAsync<GetBlocksPayload>(async data => await this.ProcessGetBlocksAsync(node, data));
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
		{
			foreach (var item in getDataPayload.Inventory.Where(inv => inv.Type == InventoryType.MSG_BLOCK))
			{
				var block = await this.blockRepository.GetAsync(item.Hash);

				if (block != null)
					await node.SendMessageAsync(new BlockPayload(block));
			}
		}

		private async Task ProcessGetBlocksAsync(Node node, GetBlocksPayload getBlocksPayload)
		{
			ChainedBlock chainedBlock = this.concurrentChain.FindFork(getBlocksPayload.BlockLocators);

			if (chainedBlock != null)
			{
				var inv = new InvPayload();
				for (var limit = 0; limit < 500; limit++)
				{
					chainedBlock = this.concurrentChain.GetBlock(chainedBlock.Height + 1);
					if (chainedBlock.HashBlock == getBlocksPayload.HashStop)
						break;

					inv.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, chainedBlock.HashBlock));
				}

				if (inv.Inventory.Any())
					await node.SendMessageAsync(inv);
			}
		}

		public override object Clone()
		{
			return new BlockStoreBehaviour(this.concurrentChain, this.blockRepository)
			{
				CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
				CanRespondeToGetDataPayload = this.CanRespondeToGetDataPayload
			};
		}
	}
}
