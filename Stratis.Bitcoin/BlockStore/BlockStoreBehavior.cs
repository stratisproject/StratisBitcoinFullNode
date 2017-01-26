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
	public class BlockStoreBehavior : NodeBehavior
	{
		private readonly ConcurrentChain concurrentChain;
		private readonly BlockRepository blockRepository;

		public bool CanRespondToGetBlocksPayload { get; set; }

		public bool CanRespondeToGetDataPayload { get; set; }

		public BlockStoreBehavior(ConcurrentChain concurrentChain, BlockRepository blockRepository)
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
			await this.AttachedNode_MessageReceivedAsync(node, message).ConfigureAwait(false);
		}

		private Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			var getDataPayload = message.Message.Payload as GetDataPayload;
			if (getDataPayload != null && this.CanRespondeToGetDataPayload)
				return this.ProcessGetDataAsync(node, getDataPayload);

			var getBlocksPayload = message.Message.Payload as GetBlocksPayload;
			if (getBlocksPayload != null && this.CanRespondToGetBlocksPayload)
				return this.ProcessGetBlocksAsync(node, getBlocksPayload);

			return Task.CompletedTask;
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
		{
			foreach (var item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_BLOCK)))
			{
				var block = await this.blockRepository.GetAsync(item.Hash).ConfigureAwait(false);

				if (block != null)
					//TODO strip block of witness if node does not support
					await node.SendMessageAsync(new BlockPayload(block.WithOptions(AttachedNode.SupportedTransactionOptions))).ConfigureAwait(false);
			}
		}

		private Task ProcessGetBlocksAsync(Node node, GetBlocksPayload getBlocksPayload)
		{
			ChainedBlock chainedBlock = this.concurrentChain.FindFork(getBlocksPayload.BlockLocators);

			if (chainedBlock == null)
				return Task.CompletedTask;

			var inv = new InvPayload();
			for (var limit = 0; limit < 500; limit++)
			{
				chainedBlock = this.concurrentChain.GetBlock(chainedBlock.Height + 1);
				if (chainedBlock.HashBlock == getBlocksPayload.HashStop)
					break;

				inv.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, chainedBlock.HashBlock));
			}

			if (inv.Inventory.Any())
				return node.SendMessageAsync(inv);

			return Task.CompletedTask;
		}

		public override object Clone()
		{
			return new BlockStoreBehavior(this.concurrentChain, this.blockRepository)
			{
				CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
				CanRespondeToGetDataPayload = this.CanRespondeToGetDataPayload
			};
		}
	}
}
