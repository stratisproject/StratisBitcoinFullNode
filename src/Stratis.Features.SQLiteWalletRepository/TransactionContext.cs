using Stratis.Features.SQLiteWalletRepository.External;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class is returned in response to a <see cref="IWalletRepository.BeginTransaction(string walletName)"/> call.
    /// It contains the <see cref="Commit"/> and <see cref="Rollback"/> methods.
    /// </summary>
    public class TransactionContext : ITransactionContext
    {
        private int transactionDepth;
        private readonly DBConnection conn;

        public TransactionContext(DBConnection conn)
        {
            this.conn = conn;
            this.transactionDepth = conn.TransactionDepth;
            conn.BeginTransaction();
        }

        public void Rollback()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Rollback();
            }
        }

        public void Commit()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Commit();
            }
        }

        public void Dispose()
        {
            while (this.conn.IsInTransaction)
            {
                this.conn.Rollback();
            }
        }
    }
}
