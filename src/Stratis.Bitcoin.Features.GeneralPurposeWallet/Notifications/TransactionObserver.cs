using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
    /// </summary>
    public class TransactionObserver : SignalObserver<Transaction>
    {
        private readonly IGeneralPurposeWalletSyncManager walletSyncManager;

        public TransactionObserver(IGeneralPurposeWalletSyncManager walletSyncManager)
        {
            this.walletSyncManager = walletSyncManager;
        }

        /// <summary>
        /// Manages what happens when a new transaction is received.
        /// </summary>
        /// <param name="transaction">The new transaction</param>
        protected override void OnNextCore(Transaction transaction)
        {
            this.walletSyncManager.ProcessTransaction(transaction);
        }
    }
}
