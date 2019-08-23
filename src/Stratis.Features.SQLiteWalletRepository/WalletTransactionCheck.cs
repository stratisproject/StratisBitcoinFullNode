using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    interface IWalletTransactionCheck
    {
        /// <summary>
        /// Determines the "tentative" or "(may) exist" collections or the wallet itself contains
        /// the given transaction id.
        /// </summary>
        /// <param name="txId">The transaction id.</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(uint256 txId);

        /// <summary>
        /// Transactions from the "tentative" collection are moved to the "(may) exist" collection
        /// if they appear in the wallet after the wallet updates have been committed.
        /// </summary>
        void Confirm();

        /// <summary>
        /// Block transactions paying to one of our addresses are added to the "(may) exist" collection.
        /// </summary>
        /// <param name="txId">The transaction id to add.</param>
        void AddTentative(uint256 txId);

        /// <summary>
        /// Looks in the given account for transactions to add to the "(may) exist" collection.
        /// </summary>
        /// <param name="walletId">The wallet to look in.</param>
        /// <param name="accountIndex">The account to look in.</param>
        void AddAll(int? walletId = null, int? accountIndex = null);
    }

    internal class TransactionsOfInterest : ObjectsOfInterest, IWalletTransactionCheck
    {
        private readonly DBConnection conn;
        private readonly int? walletId;

        internal TransactionsOfInterest(DBConnection conn, int? walletId)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        /// <inheritdoc />
        public bool Contains(uint256 txId)
        {
            return Contains(txId.ToBytes()) ?? Exists(txId);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            Confirm(o => this.Exists(new uint256(o)));
        }

        /// <inheritdoc />
        public void AddTentative(uint256 txId)
        {
            this.AddTentative(txId.ToBytes());
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
                this.Add(uint256.Parse(transactionData.OutputTxId));
        }

        private void Add(uint256 txId)
        {
            this.Add(txId.ToBytes());
        }

        private bool Exists(uint256 txId)
        {
            bool res = this.conn.ExecuteScalar<int>($@"
                SELECT EXISTS(
                    SELECT  1
                    FROM    HDTransactionData
                    WHERE   OutputTxId = ? {
                // Restrict to wallet if provided.
                ((this.walletId != null) ? $@"
                    AND     WalletId = {this.walletId}" : "")}
                    LIMIT   1);", txId.ToString()) == 1;

            return res;
        }
    }
}
