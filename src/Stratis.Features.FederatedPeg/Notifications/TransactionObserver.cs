using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
    /// </summary>
    public class TransactionObserver
    {
        private readonly IFederationWalletSyncManager walletSyncManager;
        private readonly ISignals signals;

        private readonly SubscriptionToken transactionReceivedSubscription;

        public TransactionObserver(IFederationWalletSyncManager walletSyncManager, ISignals signals)
        {
            this.walletSyncManager = walletSyncManager;
            this.signals = signals;
            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(ev => this.OnReceivingTransaction(ev.ReceivedTransaction));

            // TODO: Dispose with Detach ??
        }

        /// <summary>
        /// Manages what happens when a new transaction is received.
        /// </summary>
        /// <param name="transaction">The new transaction</param>
        private void OnReceivingTransaction(Transaction transaction)
        {
            this.walletSyncManager.ProcessTransaction(transaction);
        }
    }
}
