using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreBehavior : NodeBehavior
	{
		// Maximum number of headers to announce when relaying blocks with headers message.
		const int MAX_BLOCKS_TO_ANNOUNCE = 8;

		private readonly ConcurrentChain concurrentChain;
		private readonly BlockRepository blockRepository;

		public bool CanRespondToGetBlocksPayload { get; set; }

		public bool CanRespondeToGetDataPayload { get; set; }

		// local resources
		public Dictionary<uint256, uint256> BlockHashesToAnnounce;
		public Dictionary<uint256, uint256> InventoryBlockToSend;
		private readonly CancellationTokenSource periodicToken;
		public bool PreferHeaders; // public for testing
		private bool preferHeaderAndIDs;

		public BlockStoreBehavior(ConcurrentChain concurrentChain, BlockRepository blockRepository)
		{
			this.concurrentChain = concurrentChain;
			this.blockRepository = blockRepository;

			this.CanRespondToGetBlocksPayload = false;
			this.CanRespondeToGetDataPayload = true;
			this.periodicToken = new CancellationTokenSource();
			this.BlockHashesToAnnounce = new Dictionary<uint256, uint256>();
			this.InventoryBlockToSend = new Dictionary<uint256, uint256>();

			this.PreferHeaders = false;
			this.preferHeaderAndIDs = false;
		}

		protected override void AttachCore()
		{	
			this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			this.PeriodicTrickle(this.periodicToken.Token);
		}

		protected override void DetachCore()
		{
			this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
			this.periodicToken.Cancel();
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

			var sendCmpctPayload = message.Message.Payload as SendCmpctPayload;
			if (sendCmpctPayload != null)
				return this.ProcessSendCmpctPayload(node, sendCmpctPayload);

			var sendHeadersPayload = message.Message.Payload as SendHeadersPayload;
			if (sendHeadersPayload != null)
				this.PreferHeaders = true;

			return Task.CompletedTask;
		}

		private Task ProcessSendCmpctPayload(Node node, SendCmpctPayload sendCmpct)
		{
			// TODO: announce using compact blocks
			return Task.CompletedTask;
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
		{
			// TODO: bring logic from core 

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

		private async Task SendAsBlockInventory(Node node, IEnumerable<uint256> blocks)
		{
			var queue = new Queue<InventoryVector>(blocks.Select(s => new InventoryVector(InventoryType.MSG_BLOCK, s)));
			while (queue.Count > 0)
			{
				var items = queue.TakeAndRemove(ConnectionManager.MAX_INV_SZ).ToArray();
				if (node.IsConnected)
					await node.SendMessageAsync(new InvPayload(items));
			}
		}

		private Task AnnounceBlocks(Node node)
		{
			bool revertToInv = ((!this.PreferHeaders &&
			                     (!this.preferHeaderAndIDs || this.BlockHashesToAnnounce.Count > 1)) ||
			                    this.BlockHashesToAnnounce.Count > MAX_BLOCKS_TO_ANNOUNCE);

			var headers = new List<BlockHeader>();
			var blocks = this.BlockHashesToAnnounce.Keys.ToList();
			if (blocks.Any())
			{
				foreach (var blockHash in blocks)
					this.BlockHashesToAnnounce.Remove(blockHash);

				var chainBehavior = node.Behavior<ChainBehavior>();

				if (!revertToInv)
				{
					bool foundStartingHeader = false;
					// Try to find first header that our peer doesn't have, and
					// then send all headers past that one.  If we come across any
					// headers that aren't on chainActive, give up.

					foreach (var hash in blocks)
					{
						var chainedBlock = this.concurrentChain.GetBlock(hash);
						if (chainedBlock == null)
						{
							// Bail out if we reorged away from this block
							revertToInv = true;
							break;
						}
						if (foundStartingHeader)
							headers.Add(chainedBlock.Header);
						else if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) != null)
							continue;
						else if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Previous.Height) != null)
						{
							// Peer doesn't have this header but they do have the prior one.
							// Start sending headers.
							foundStartingHeader = true;
							headers.Add(chainedBlock.Header);
						}
						else
						{
							// Peer doesn't have this header or the prior one -- nothing will
							// connect, so bail out.
							revertToInv = true;
							break;
						}
					}
				}

				if (!revertToInv && headers.Any())
				{
					if (headers.Count == 1 && this.preferHeaderAndIDs)
					{
						// TODO:
					}
					else if (this.PreferHeaders)
					{
						if (headers.Count > 1)
						{
							Logging.Logs.BlockStore.LogInformation(
								$"{headers.Count} headers, range ({headers.First()}, {headers.Last()}), to peer={node.PeerVersion.Nonce}");
						}
						else
						{
							Logging.Logs.BlockStore.LogInformation(
								$"sending header ({headers.First()}), to peer={node.PeerVersion.Nonce}");
						}
						return node.SendMessageAsync(new HeadersPayload(headers.ToArray()));
					}
					else
					{
						revertToInv = true;
					}
				}

				if (revertToInv)
				{
					// If falling back to using an inv, just try to inv the tip.
					// The last entry in vBlockHashesToAnnounce was our tip at some point
					// in the past.

					if (blocks.Any())
					{
						var hashToAnnounce = blocks.Last();
						var chainedBlock = this.concurrentChain.GetBlock(hashToAnnounce);
						if (chainedBlock != null)
						{
							if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) == null)
							{
								this.InventoryBlockToSend.TryAdd(hashToAnnounce, hashToAnnounce);
								Logging.Logs.BlockStore.LogInformation($"sending inv peer={node.PeerVersion.Nonce} hash={hashToAnnounce}");
							}
						}
					}
				}
			}

			var toSend = this.InventoryBlockToSend.Keys.ToList();
			if (toSend.Any())
			{
				foreach (var blockHash in toSend)
					this.InventoryBlockToSend.Remove(blockHash);
				return this.SendAsBlockInventory(node, toSend);
			}

			return Task.CompletedTask;
		}

		public Task PeriodicTrickle(CancellationToken token)
		{
			// run on the Default scheduler
			return Task.Run(async () =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						if (this.AttachedNode != null)
							await this.AnnounceBlocks(this.AttachedNode);

						await Task.Delay(1000, token);
					}
					catch (OperationCanceledException opx)
					{
						if (!opx.CancellationToken.IsCancellationRequested)
							if (this.AttachedNode?.IsConnected ?? false)
								throw;

						// do nothing
					}
					catch (Exception)
					{
						// handle this so it doesn't hit the execution context
					}
				}

			}, token); // unwrap doesn't matter much as no one will be listening/awaiting
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
