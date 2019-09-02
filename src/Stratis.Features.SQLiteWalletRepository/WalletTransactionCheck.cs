using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    interface IWalletTransactionCheck
    {
        /// <summary>
        /// Determines whether the "tentative" or "(may) exist" collections or the wallet itself contains
        /// the given outpoint.
        /// </summary>
        /// <param name="outPoint">The transaction id.</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(OutPoint outPoint);

        /// <summary>
        /// Transactions from the "tentative" collection are moved to the "(may) exist" collection
        /// if they appear in the wallet after the wallet updates have been committed.
        /// </summary>
        void Confirm();

        /// <summary>
        /// Outpoints paying to one of our addresses are added to the "(may) exist" collection.
        /// </summary>
        /// <param name="outPoint">The transaction id to add.</param>
        void AddTentative(OutPoint outPoint);

        /// <summary>
        /// Looks in the given account for outpoints to add to the "(may) exist" collection.
        /// </summary>
        /// <param name="walletId">The wallet to look in.</param>
        /// <param name="accountIndex">The account to look in.</param>
        void AddAll(int? walletId = null, int? accountIndex = null);
    }

    internal class TransactionsOfInterest : ObjectsOfInterest, IWalletTransactionCheck
    {
        private readonly DBConnection conn;
        private readonly int? walletId;

        internal TransactionsOfInterest(DBConnection conn, int? walletId) :
            // Create a bigger hash table if its shared.
            // TODO: Make this configurable.
            base(conn.Repository.DatabasePerWallet ? 20 : 26)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        /// <inheritdoc />
        public bool Contains(OutPoint outPoint)
        {
            return Contains(outPoint.ToBytes()) ?? Exists(outPoint);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            Confirm(o => { var x = new OutPoint(); x.FromBytes(o); return this.Exists(x); });
        }

        /// <inheritdoc />
        public void AddTentative(OutPoint outPoint)
        {
            this.AddTentative(outPoint.ToBytes());
        }

        /// <inheritdoc />
        public void AddAll(int? walletId = null, int? accountIndex = null)
        {
            Guard.Assert((walletId ?? this.walletId) == (this.walletId ?? walletId));

            walletId = this.walletId ?? walletId;

            List<HDTransactionData> spendableTransactions = this.conn.Query<HDTransactionData>($@"
                SELECT  *
                FROM    HDTransactionData
                WHERE   SpendBlockHash IS NULL
                AND     SpendBlockHeight IS NULL {
                // Restrict to wallet if provided.
                ((walletId != null) ? $@"
                AND      WalletId = {walletId}" : "")}{
                // Restrict to account if provided.
                ((accountIndex != null) ? $@"
                AND     AccountIndex = {accountIndex}" : "")}");

            foreach (HDTransactionData transactionData in spendableTransactions)
                this.Add(new OutPoint(uint256.Parse(transactionData.OutputTxId), transactionData.OutputIndex));
        }

        private void Add(OutPoint outPoint)
        {
            this.Add(outPoint.ToBytes());
        }

        private bool Exists(OutPoint outPoint)
        {
            bool res = this.conn.ExecuteScalar<int>($@"
                SELECT EXISTS(
                    SELECT  1
                    FROM    HDTransactionData
                    WHERE   OutputTxId = ?
                    AND     OutputIndex = ? {
                // Restrict to wallet if provided.
                ((this.walletId != null) ? $@"
                    AND     WalletId = {this.walletId}" : "")}
                    LIMIT   1);", outPoint.Hash.ToString(), outPoint.N) == 1;

            return res;
        }
    }
}
