using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
	public interface IBlockStoreBehavior : INodeBehavior
	{
		bool CanRespondeToGetDataPayload { get; set; }
		bool CanRespondToGetBlocksPayload { get; set; }
		Task AnnounceBlocks(List<uint256> blockHashesToAnnounce);
	}

	public class BlockStoreBehavior : NodeBehavior
	{
        // TODO: move this to the options
	    // Maximum number of headers to announce when relaying blocks with headers message.
		const int MAX_BLOCKS_TO_ANNOUNCE = 8;

		private readonly ConcurrentChain chain;
		private readonly IBlockRepository blockRepository;
		private readonly IBlockStoreCache blockStoreCache;
	    private readonly ILogger logger;

        public bool CanRespondToGetBlocksPayload { get; set; }

		public bool CanRespondeToGetDataPayload { get; set; }

		// local resources
		public bool PreferHeaders; // public for testing
		private bool preferHeaderAndIDs;

		public BlockStoreBehavior(
            ConcurrentChain chain, 
            BlockRepository blockRepository, 
            IBlockStoreCache blockStoreCache, 
            ILogger logger)
			: this(chain, blockRepository as IBlockRepository, blockStoreCache, logger)
		{
		}

		public BlockStoreBehavior(ConcurrentChain chain, IBlockRepository blockRepository, IBlockStoreCache blockStoreCache, ILogger logger)
		{
			Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(blockRepository, nameof(blockRepository));
			Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
		    Guard.NotNull(blockStoreCache, nameof(logger));

            this.chain = chain;
			this.blockRepository = blockRepository;
			this.blockStoreCache = blockStoreCache;
		    this.logger = logger;

            this.CanRespondToGetBlocksPayload = false;
			this.CanRespondeToGetDataPayload = true;

			this.PreferHeaders = false;
			this.preferHeaderAndIDs = false;
		}

		protected override void AttachCore()
		{
			this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
		}

		protected override void DetachCore()
		{
			this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
		}

		private async void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			try
			{
				await this.AttachedNode_MessageReceivedAsync(node, message).ConfigureAwait(false);
			}
			catch (OperationCanceledException opx)
			{
				if (!opx.CancellationToken.IsCancellationRequested)
					if (this.AttachedNode?.IsConnected ?? false)
						throw;

				// do nothing
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex.ToString());

				// while in dev catch any unhandled exceptions
				Debugger.Break();
				throw;
			}
		}

		private Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			var getDataPayload = message.Message.Payload as GetDataPayload;
			if (getDataPayload != null && this.CanRespondeToGetDataPayload)
				return this.ProcessGetDataAsync(node, getDataPayload);

			// TODO: this is not used in core anymore consider deleting it
			////var getBlocksPayload = message.Message.Payload as GetBlocksPayload;
			////if (getBlocksPayload != null && this.CanRespondToGetBlocksPayload)
			////	return this.ProcessGetBlocksAsync(node, getBlocksPayload);

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
			Guard.Assert(node != null);

			// TODO: bring logic from core 
			foreach (var item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_BLOCK)))
			{
				// TODO: check if we need to add support for "not found" 

				var block = await this.blockStoreCache.GetBlockAsync(item.Hash).ConfigureAwait(false);


				if (block != null)
					//TODO strip block of witness if node does not support
					await node.SendMessageAsync(new BlockPayload(block.WithOptions(node.SupportedTransactionOptions))).ConfigureAwait(false);
			}
		}

		////private Task ProcessGetBlocksAsync(Node node, GetBlocksPayload getBlocksPayload)
		////{
		////	ChainedBlock chainedBlock = this.chain.FindFork(getBlocksPayload.BlockLocators);

		////	if (chainedBlock == null)
		////		return Task.CompletedTask;

		////	var inv = new InvPayload();
		////	for (var limit = 0; limit < 500; limit++)
		////	{
		////		chainedBlock = this.chain.GetBlock(chainedBlock.Height + 1);
		////		if (chainedBlock.HashBlock == getBlocksPayload.HashStop)
		////			break;

		////		inv.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, chainedBlock.HashBlock));
		////	}

		////	if (inv.Inventory.Any())
		////		return node.SendMessageAsync(inv);

		////	return Task.CompletedTask;
		////}

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

		public Task AnnounceBlocks(List<uint256> blockHashesToAnnounce)
		{
			Guard.NotNull(blockHashesToAnnounce, nameof(blockHashesToAnnounce));

			if (!blockHashesToAnnounce.Any())
				return Task.CompletedTask;

			var node = this.AttachedNode;
			if (node == null)
				return Task.CompletedTask;

			bool revertToInv = ((!this.PreferHeaders &&
								 (!this.preferHeaderAndIDs || blockHashesToAnnounce.Count > 1)) ||
								blockHashesToAnnounce.Count > MAX_BLOCKS_TO_ANNOUNCE);

			var headers = new List<BlockHeader>();
			var inventoryBlockToSend = new List<uint256>();

			var chainBehavior = node.Behavior<ChainHeadersBehavior>();
			ChainedBlock bestIndex = null;
			if (!revertToInv)
			{
				bool foundStartingHeader = false;
				// Try to find first header that our peer doesn't have, and
				// then send all headers past that one.  If we come across any
				// headers that aren't on chainActive, give up.

				foreach (var hash in blockHashesToAnnounce)
				{
					var chainedBlock = this.chain.GetBlock(hash);
					if (chainedBlock == null)
					{
						// Bail out if we reorged away from this block
						revertToInv = true;
						break;
					}
					bestIndex = chainedBlock;
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
					    this.logger.LogInformation(
							$"{headers.Count} headers, range ({headers.First()}, {headers.Last()}), to peer={node.RemoteSocketEndpoint}");
					}
					else
					{
					    this.logger.LogInformation(
							$"sending header ({headers.First()}), to peer={node.RemoteSocketEndpoint}");
					}

					chainBehavior.SetPendingTip(bestIndex);
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

				if (blockHashesToAnnounce.Any())
				{
					var hashToAnnounce = blockHashesToAnnounce.Last();
					var chainedBlock = this.chain.GetBlock(hashToAnnounce);
					if (chainedBlock != null)
					{
						if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) == null)
						{
							inventoryBlockToSend.Add(hashToAnnounce);
						    this.logger.LogInformation($"sending inv peer={node.RemoteSocketEndpoint} hash={hashToAnnounce}");
						}
					}
				}
			}

			if (inventoryBlockToSend.Any())
			{
				return this.SendAsBlockInventory(node, inventoryBlockToSend);
			}

			return Task.CompletedTask;
		}

		public override object Clone()
		{
			return new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.logger)
			{
				CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
				CanRespondeToGetDataPayload = this.CanRespondeToGetDataPayload
			};
		}
	}
}
