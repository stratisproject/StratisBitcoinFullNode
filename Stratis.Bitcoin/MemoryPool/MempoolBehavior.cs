using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolBehavior : NodeBehavior
	{
		private readonly MempoolValidator validator;
		private readonly MempoolManager manager;
		private readonly MempoolOrphans orphans;

		public bool CanRespondToMempool { get; set; }

		public MempoolBehavior(MempoolValidator validator, MempoolManager manager, MempoolOrphans orphans)
		{
			this.validator = validator;
			this.manager = manager;
			this.orphans = orphans;

			this.CanRespondToMempool = true;
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
			var txPayload = message.Message.Payload as TxPayload;
			if (txPayload != null)
				return this.ProcessTxPayloadAsync(node, txPayload);

			var mempoolPayload = message.Message.Payload as MempoolPayload;
			if (mempoolPayload != null && this.CanRespondToMempool)
				return this.manager.GetMempoolAsync();

			return Task.CompletedTask;
		}

		private async Task ProcessTxPayloadAsync(Node node, TxPayload transactionPayload)
		{
			var trx = transactionPayload.Object;
			var state = new MemepoolValidationState(true);
			if (!await this.orphans.AlreadyHave(trx.GetHash()) && await this.validator.AcceptToMemoryPool(state, trx))
			{
				await this.validator.SanityCheck();
				await this.RelayTransaction(trx).ConfigureAwait(false);
				var mmsize = await this.manager.MempoolSize();
				var memdyn = await this.manager.MempoolDynamicMemoryUsage();
				Logging.Logs.Mempool.LogInformation(
					$"AcceptToMemoryPool: peer={node.PeerVersion.Nonce}: accepted {trx.GetHash()} (poolsz {mmsize} txn, {memdyn/ 1000} kb)");

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
				Logging.Logs.Mempool.LogInformation($"{trx.GetHash()} from peer={node.PeerVersion.Nonce} was not accepted: {state}");
			}
		}

		public Task RelayTransaction(NBitcoin.Transaction tx)
		{
			// TODO: Relay inventory
			// To relay the transaction their needs to be a track of inventory already relayed on each node
			// this could be implemented using a RelayBehaviour on each node that tracks inv messages

			//var managerBehavior = this.AttachedNode.Behaviors.Find<ConnectionManagerBehavior>();
			//var nodes = managerBehavior?.ConnectionManager?.ConnectedNodes ?? Enumerable.Empty<Node>();
			//foreach (var node in nodes)
			//{
			//	await node.SendMessageAsync(new TxPayload(tx)).ConfigureAwait(false);
			//}

			return Task.CompletedTask;
		}

		public override object Clone()
		{
			return new MempoolBehavior(this.validator, this.manager, this.orphans)
			{
				CanRespondToMempool = this.CanRespondToMempool
			};
		}
	}
}
