using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolBehavior : NodeBehavior
	{
		// Average delay between trickled inventory transmissions in seconds.
		//  Blocks and whitelisted receivers bypass this, outbound peers get half this delay. 
		const int INVENTORY_BROADCAST_INTERVAL = 5;
		// Maximum number of inventory items to send per transmission.
		//  Limits the impact of low-fee transaction floods. 
		const int INVENTORY_BROADCAST_MAX = 7*INVENTORY_BROADCAST_INTERVAL;

		private readonly MempoolValidator validator;
		private readonly MempoolManager manager;
		private readonly MempoolOrphans orphans;
		private readonly ConnectionManager connectionManager;

		public long LastMempoolReq { get; private set; }
		public long NextInvSend { get; set; }

		// state that is local to the behaviour
		private readonly Dictionary<uint256, uint256> inventoryTxToSend;
		private readonly Dictionary<uint256, uint256> filterInventoryKnown;

		private readonly CancellationTokenSource periodicToken;

		public MempoolBehavior(MempoolValidator validator, MempoolManager manager, MempoolOrphans orphans, ConnectionManager connectionManager)
		{
			this.validator = validator;
			this.manager = manager;
			this.orphans = orphans;
			this.connectionManager = connectionManager;

			this.inventoryTxToSend = new Dictionary<uint256, uint256>();
			this.filterInventoryKnown = new Dictionary<uint256, uint256>();
			this.periodicToken = CancellationTokenSource.CreateLinkedTokenSource(new[] {this.manager.MempoolScheduler.Cancellation.Token});
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
			// TODO: Add exception handling
			// TODO: this should probably be on the mempool scheduler and wrapped in a try/catch
			// async void methods are considered fire and forget and not catch exceptions (typical for event handlers)
			// Exceptions will bubble to the execution context, we don't want that!

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
				Logging.Logs.Mempool.LogError(ex.ToString());

				// while in dev catch any unhandled exceptions
				Debugger.Break();
			}
			
		}

		private Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
		{
			var txPayload = message.Message.Payload as TxPayload;
			if (txPayload != null)
				return this.ProcessTxPayloadAsync(node, txPayload);

			var mempoolPayload = message.Message.Payload as MempoolPayload;
			if (mempoolPayload != null)
				return this.SendMempoolPayload(node, mempoolPayload);

			var getDataPayload = message.Message.Payload as GetDataPayload;
			if (getDataPayload != null)
				return this.ProcessGetDataAsync(node, getDataPayload);

			var invPayload = message.Message.Payload as InvPayload;
			if (invPayload != null)
				return this.ProcessInvAsync(node, invPayload);

			return Task.CompletedTask;
		}

		private async Task ProcessInvAsync(Node node, InvPayload invPayload)
		{
			Check.Assert(node == this.AttachedNode); // just in case

			if (invPayload.Inventory.Count > ConnectionManager.MAX_INV_SZ)
			{
				//Misbehaving(pfrom->GetId(), 20); // TODO: Misbehaving
				return; //error("message inv size() = %u", vInv.size());
			}

			if(this.manager.IsInitialBlockDownload)
				return;

			bool blocksOnly = !this.manager.NodeArgs.Mempool.RelayTxes;
			// Allow whitelisted peers to send data other than blocks in blocks only mode if whitelistrelay is true
			if (node.Behavior<ConnectionManagerBehavior>().Whitelisted && this.manager.NodeArgs.Mempool.Whitelistrelay)
				blocksOnly = false;

			//uint32_t nFetchFlags = GetFetchFlags(pfrom, chainActive.Tip(), chainparams.GetConsensus());

			var send = new GetDataPayload();
			foreach (var inv in invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				//inv.type |= nFetchFlags;

				if (blocksOnly)
					Logging.Logs.Mempool.LogInformation(
						$"transaction ({inv.Hash}) inv sent in violation of protocol peer={node.PeerVersion.Nonce}");

				if (await this.orphans.AlreadyHave(inv.Hash))
					continue;

				send.Inventory.Add(inv);
			}

			// add to known inventory
			await this.manager.MempoolScheduler.DoExclusive(() =>
			{
				foreach (var inventoryVector in send.Inventory)
					this.filterInventoryKnown.TryAdd(inventoryVector.Hash, inventoryVector.Hash);
			});

			if (node.IsConnected)
				await node.SendMessageAsync(send).ConfigureAwait(false);
		}

		private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
		{
			Check.Assert(node == this.AttachedNode); // just in case

			foreach (var item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX)))
			{
				var trxInfo = await this.manager.InfoAsync(item.Hash).ConfigureAwait(false);

				if (trxInfo != null)
					//TODO strip block of witness if node does not support
					if (node.IsConnected)
						await node.SendMessageAsync(new TxPayload(trxInfo.Trx.WithOptions(node.SupportedTransactionOptions))).ConfigureAwait(false);
			}
		}

		private async Task ProcessTxPayloadAsync(Node node, TxPayload transactionPayload)
		{
			var trx = transactionPayload.Object;
			var trxHash = trx.GetHash();

			// add to local filter
			await this.manager.MempoolScheduler.DoExclusive(() => this.filterInventoryKnown.TryAdd(trxHash, trxHash));

			var state = new MemepoolValidationState(true);
			if (!await this.orphans.AlreadyHave(trxHash) && await this.validator.AcceptToMemoryPool(state, trx))
			{
				await this.validator.SanityCheck();
				await this.RelayTransaction(trxHash).ConfigureAwait(false);

				var mmsize = state.MempoolSize;
				var memdyn = state.MempoolDynamicSize;
				Logging.Logs.Mempool.LogInformation(
					$"AcceptToMemoryPool: peer={node.PeerVersion.Nonce}: accepted {trxHash} (poolsz {mmsize} txn, {memdyn/1000} kb)");

				await this.orphans.ProcessesOrphans(this, trx);
			}
			else if (state.MissingInputs)
			{
				await this.orphans.ProcessesOrphansMissingInputs(node, trx);
			}
			else
			{
				if (!trx.HasWitness && state.CorruptionPossible)
				{

				}

				// TODO: Implement Processes whitelistforcerelay
			}

			if (state.IsInvalid)
			{
				Logging.Logs.Mempool.LogInformation($"{trxHash} from peer={node.PeerVersion.Nonce} was not accepted: {state}");
			}
		}

		public Task RelayTransaction(uint256 hash)
		{
			var nodes = this.connectionManager.ConnectedNodes;
			if (!nodes.Any())
				return Task.CompletedTask;

			// find all behaviours then start an exclusive task 
			// to add the hash to each local collection
			var behaviours = nodes.Select(s => s.Behavior<MempoolBehavior>());
			return this.manager.MempoolScheduler.DoExclusive(() =>
			{
				foreach (var mempoolBehavior in behaviours)
				{
					if (mempoolBehavior?.AttachedNode.PeerVersion.Relay ?? false)
						if (!mempoolBehavior.filterInventoryKnown.ContainsKey(hash))
							mempoolBehavior.inventoryTxToSend.TryAdd(hash, hash);
				}
			});
		}

		/// <summary>
		/// Send the memory pool payload to the attached node
		/// </summary>
		public async Task SendMempoolPayload(Node node, MempoolPayload message)
		{
			Check.Assert(node == this.AttachedNode); // just in case

			if (!this.CanSendTrickle)
				return;

			//if (!(pfrom->GetLocalServices() & NODE_BLOOM) && !pfrom->fWhitelisted)
			//{
			//	LogPrint("net", "mempool request with bloom filters disabled, disconnect peer=%d\n", pfrom->GetId());
			//	pfrom->fDisconnect = true;
			//	return true;
			//}

			//if (connman.OutboundTargetReached(false) && !pfrom->fWhitelisted)
			//{
			//	LogPrint("net", "mempool request with bandwidth limit reached, disconnect peer=%d\n", pfrom->GetId());
			//	pfrom->fDisconnect = true;
			//	return true;
			//}

			var vtxinfo = await this.manager.InfoAllAsync();
			var filterrate = Money.Zero;

			// TODO: implement minFeeFilter
			//{
			//	LOCK(pto->cs_feeFilter);
			//	filterrate = pto->minFeeFilter;
			//}

			var sends = await this.manager.MempoolScheduler.DoExclusive(() =>
			{
				var ret = new List<TxMempoolInfo>();
				foreach (var txinfo in vtxinfo)
				{
					var hash = txinfo.Trx.GetHash();
					this.inventoryTxToSend.Remove(hash);
					if (filterrate != Money.Zero)
						if (txinfo.FeeRate.FeePerK < filterrate)
							continue;
					filterInventoryKnown.TryAdd(hash, hash);
				}
				return ret;
			});

			await this.SendAsTxInventory(node, sends.Select(s => s.Trx.GetHash()));
			this.LastMempoolReq = this.manager.DateTimeProvider.GetTime();
		}

		private async Task SendAsTxInventory(Node node, IEnumerable<uint256> trxList)
		{
			var queue = new Queue<InventoryVector>(trxList.Select(s => new InventoryVector(InventoryType.MSG_TX, s)));
			while (queue.Count > 0)
			{
				var items = queue.TakeAndRemove(ConnectionManager.MAX_INV_SZ).ToArray();
				if (node.IsConnected)
					await node.SendMessageAsync(new InvPayload(items));
			}
		}

		public bool CanSendTrickle
		{
			get
			{
				if (!this.AttachedNode?.PeerVersion?.Relay ?? true)
					return false;

				// Check whether periodic sends should happen
				var sendTrickle = this.AttachedNode.Behavior<ConnectionManagerBehavior>().Whitelisted;

				if (this.NextInvSend < this.manager.DateTimeProvider.GetTime())
				{
					sendTrickle = true;
					// Use half the delay for outbound peers, as there is less privacy concern for them.
					this.NextInvSend = this.manager.DateTimeProvider.GetTime() + TimeSpan.TicksPerMinute;
					// TODO: PoissonNextSend(nNow, INVENTORY_BROADCAST_INTERVAL >> !pto->fInbound);
				}

				return sendTrickle;
			}
		}

		public async Task SendTrickle()
		{
			var sends = await this.manager.MempoolScheduler.DoExclusive(() =>
			{
				// Determine transactions to relay
				// Produce a vector with all candidates for sending
				var invs = this.inventoryTxToSend.Keys.Take(INVENTORY_BROADCAST_MAX).ToList();
				var ret = new List<uint256>();
				foreach (var hash in invs)
				{
					// Remove it from the to-be-sent set
					this.inventoryTxToSend.Remove(hash);
					// Check if not in the filter already
					if (this.filterInventoryKnown.ContainsKey(hash))
						continue;
					// Not in the mempool anymore? don't bother sending it.
					var txInfo = this.manager.Info(hash);
					if (txInfo == null)
						continue;
					//if (filterrate && txinfo.feeRate.GetFeePerK() < filterrate) // TODO:filterrate
					//{
					//	continue;
					//}
					ret.Add(hash);
				}
				return ret;
			});

			if (sends.Any())
				await this.SendAsTxInventory(this.AttachedNode, sends);
		}

		public Task PeriodicTrickle(CancellationToken token)
		{
			// hope on the mempool scheduler (which is the Default scheduler)
			return this.manager.MempoolScheduler.DoConcurrent(async () =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						if (this.CanSendTrickle)
							await this.SendTrickle();

						// TODO: this can be smarter by making the wait time similar to the PoissonNextSend result
						await Task.Delay(10000, token); 
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
				
			}).Unwrap(); // unwrap doesn't matter much as no one will be listening/awaiting
		}

		public override object Clone()
		{
			return new MempoolBehavior(this.validator, this.manager, this.orphans, this.connectionManager);
		}
	}
}
