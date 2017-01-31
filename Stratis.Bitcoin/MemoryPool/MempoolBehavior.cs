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

		public bool CanRespondToMempool { get; set; }

		public MempoolBehavior(MempoolValidator validator, MempoolManager manager)
		{
			this.validator = validator;
			this.manager = manager;

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
			if (!await this.manager.AlreadyHave(trx) && await this.validator.AcceptToMemoryPool(state, trx))
			{
				await this.validator.SanityCheck();
				await this.RelayTransaction(trx).ConfigureAwait(false);
				var mmsize = await this.manager.MempoolSize();
				var memdyn = await this.manager.MempoolDynamicMemoryUsage();
				Logging.Logs.Mempool.LogInformation(
					$"AcceptToMemoryPool: peer={node.PeerVersion.Nonce}: accepted {trx.GetHash()} (poolsz {mmsize} txn, {memdyn/ 1000} kb)");

				// TODO: Implement OrphanTransactions 
				// Recursively process any orphan transactions that depended on this one

			}
			else if (state.MissingInputs)
			{
				// TODO: Implement OrphanTransactions (processes MissingInputs)
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

		private Task RelayTransaction(NBitcoin.Transaction tx)
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
			return new MempoolBehavior(this.validator, this.manager)
			{
				CanRespondToMempool = this.CanRespondToMempool
			};
		}
	}
}
