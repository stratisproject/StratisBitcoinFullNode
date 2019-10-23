using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class is returned in response to a <see cref="IWalletRepository.BeginTransaction(string walletName)"/> call.
    /// It contains the <see cref="Commit"/> and <see cref="Rollback"/> methods.
    /// </summary>
    public class TransactionContext : ITransactionContext
    {
        private int transactionDepth;
        private readonly WalletContainer walletContainer;

        internal TransactionContext(WalletContainer walletContainer)
        {
            this.walletContainer = walletContainer;
            this.transactionDepth = walletContainer.Conn.TransactionDepth;
            walletContainer.Conn.BeginTransaction();
        }

        public void Rollback()
        {
            while (this.walletContainer.Conn.IsInTransaction)
            {
                this.walletContainer.Conn.Rollback();
            }

            this.walletContainer.LockUpdateWallet.Release();
        }

        public void Commit()
        {
            while (this.walletContainer.Conn.IsInTransaction)
            {
                this.walletContainer.Conn.Commit();
            }

            this.walletContainer.LockUpdateWallet.Release();
        }

        public void Dispose()
        {
            while (this.walletContainer.Conn.IsInTransaction)
            {
                this.walletContainer.Conn.Rollback();
            }

            this.walletContainer.LockUpdateWallet.Release();
        }
    }
}
