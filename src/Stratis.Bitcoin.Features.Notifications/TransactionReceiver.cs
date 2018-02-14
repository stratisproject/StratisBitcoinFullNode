using System;
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

            this.SubscribeToPayload<InvPayload>((payload, peer) => this.ProcessPayloadAndHandleErrors(payload, peer, this.logger, this.ProcessInvAsync));
            this.SubscribeToPayload<TxPayload>((payload, peer) => this.ProcessPayloadAndHandleErrors(payload, peer, this.logger, this.ProcessTxPayload));
        }

        private async Task ProcessTxPayload(TxPayload txPayload, INetworkPeer peer)
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

        private async Task ProcessInvAsync(InvPayload invPayload, INetworkPeer peer)
        {
            var txs = invPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_TX));

            // get the transactions in this inventory that have never been received - either because new or requested.
            var newTxs = txs.Where(t => this.notifiedTransactions.TransactionsReceived.All(ts => ts.Key != t.Hash)).ToList();

            if (!newTxs.Any())
            {
                return;
            }

            // requests the new transactions
            if (peer.IsConnected)
            {
                await peer.SendMessageAsync(new GetDataPayload(newTxs.ToArray())).ConfigureAwait(false);
            }
        }

        public override object Clone()
        {
            return new TransactionReceiver(this.transactionNotification, this.notifiedTransactions, this.logger);
        }
    }
}