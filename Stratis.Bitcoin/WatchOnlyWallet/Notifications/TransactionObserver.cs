using NBitcoin;
using Stratis.Bitcoin;

namespace Stratis.Bitcoin.WatchOnlyWallet
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
    /// </summary>
	public class TransactionObserver : SignalObserver<Transaction>
    {
        private readonly IWatchOnlyWalletManager walletManager;

        public TransactionObserver(IWatchOnlyWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Manages what happens when a new transaction is received.
        /// </summary>
        /// <param name="transaction">The new transaction</param>
        protected override void OnNextCore(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }
    }
}
