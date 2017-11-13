using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// This class receives transaction messages from other nodes.
    /// </summary>
    public class TransactionReceiver : NodeBehavior
    {
        private readonly TransactionNotification transactionNotification;
        private readonly TransactionNotificationProgress notifiedTransactions;
        private readonly ILogger logger;

        public TransactionReceiver(TransactionNotification transactionNotification, TransactionNotificationProgress notifiedTransactions, ILoggerFactory loggerFactory)
            : this(transactionNotification, notifiedTransactions, loggerFactory.CreateLogger(typeof(TransactionReceiver).FullName))
        {

        }

        public TransactionReceiver(TransactionNotification transactionNotification, TransactionNotificationProgress notifiedTransactions, ILogger logger)
        {
            this.transactionNotification = transactionNotification;
            this.notifiedTransactions = notifiedTransactions;
            this.logger = logger;
        }

        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceivedAsync;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceivedAsync;
        }

        private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
        {
            try
            {
                //Guard.Assert(node == this.AttachedNode); // just in case
                await this.ProcessMessageAsync(node, message).ConfigureAwait(false);
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

        private Task ProcessMessageAsync(Node node, IncomingMessage message)
        {
            // check the type of message received.
            // we're only interested in Inventory and Transaction messages.
            var invPayload = message.Message.Payload as InvPayload;
            if (invPayload != null)
            {
                return this.ProcessInvAsync(node, invPayload);
            }

            var txPayload = message.Message.Payload as TxPayload;
            if (txPayload != null)
            {
                this.ProcessTxPayload(txPayload);
            }

            return Task.CompletedTask;
        }

        private void ProcessTxPayload(TxPayload txPayload)
        {
            var transaction = txPayload.Obj;
            var trxHash = transaction.GetHash();

            if (this.notifiedTransactions.TransactionsReceived.ContainsKey(trxHash))
            {
                return;
            }

            // send the transaction to the notifier
            this.transactionNotification.Notify(transaction);
            this.notifiedTransactions.TransactionsReceived.TryAdd(trxHash, trxHash);
        }

        private async Task ProcessInvAsync(Node node, InvPayload invPayload)
        {
            var txs = invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX));

            // get the transactions in this inventory that have never been received - either because new or requested.
            var newTxs = txs.Where(t => this.notifiedTransactions.TransactionsReceived.All(ts => ts.Key != t.Hash)).ToList();

            if (!newTxs.Any())
            {
                return;
            }

            // requests the new transactions
            if (node.IsConnected)
            {
                await node.SendMessageAsync(new GetDataPayload(newTxs.ToArray())).ConfigureAwait(false);
            }
        }

        public override object Clone()
        {
            return new TransactionReceiver(this.transactionNotification, this.notifiedTransactions, this.logger);
        }
    }
}
