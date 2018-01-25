﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// This class receives transaction messages from other nodes.
    /// </summary>
    public class TransactionReceiver : NetworkPeerBehavior
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
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        private async Task OnMessageReceivedAsync(NetworkPeer peer, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());

                // while in dev catch any unhandled exceptions
                Debugger.Break();
                throw;
            }
        }

        private async Task ProcessMessageAsync(NetworkPeer peer, IncomingMessage message)
        {
            // Check the type of message received.
            // We're only interested in Inventory and Transaction messages.
            switch (message.Message.Payload)
            {
                case InvPayload invPayload:
                    await this.ProcessInvAsync(peer, invPayload).ConfigureAwait(false);
                    break;

                case TxPayload txPayload:
                    this.ProcessTxPayload(txPayload);
                    break;
            }
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

        private async Task ProcessInvAsync(NetworkPeer node, InvPayload invPayload)
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
