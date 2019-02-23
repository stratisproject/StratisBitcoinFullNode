using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
    /// </summary>
    public class TransactionObserver
    {
        private readonly IFederationWalletSyncManager walletSyncManager;
        private readonly ISignals signals;

        public TransactionObserver(IFederationWalletSyncManager walletSyncManager, ISignals signals)
        {
            this.walletSyncManager = walletSyncManager;
            this.signals = signals;
            this.signals.OnTransactionReceived.Attach(this.OnReceivingTransaction);

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
