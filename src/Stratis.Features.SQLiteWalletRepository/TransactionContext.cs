using System.IO;
using System.Linq;
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

            this.CleanupUnusedWalletFile();

            this.walletContainer.LockUpdateWallet.Release();
        }

        internal void CleanupUnusedWalletFile()
        {
            if (this.walletContainer.Wallet == null)
            {
                SQLiteWalletRepository repo = this.walletContainer.Conn.Repository;

                if (repo.DatabasePerWallet)
                {
                    string walletName = repo.Wallets.FirstOrDefault(w => ReferenceEquals(w.Value, this.walletContainer)).Key;
                    this.walletContainer.Conn.SQLiteConnection.Dispose();
                    if (walletName != null)
                        File.Delete(Path.Combine(repo.DBPath, $"{walletName}.db"));
                }
            }
        }

        public void Commit()
        {
            while (this.walletContainer.Conn.IsInTransaction)
            {
                this.walletContainer.Conn.Commit();
            }

            this.CleanupUnusedWalletFile();

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
