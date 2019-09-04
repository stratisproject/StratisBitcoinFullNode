using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    public class AddressIdentifier
    {
        public int WalletId { get; set; }
        public int AccountIndex { get; set; }
        public int AddressType { get; set; }
        public int AddressIndex { get; set; }
        public string ScriptPubKey { get; set; }

        public override bool Equals(object obj)
        {
            var address = (AddressIdentifier)obj;
            return this.WalletId == address.WalletId &&
                this.AccountIndex == address.AccountIndex &&
                this.AddressType == address.AddressType &&
                this.AddressIndex == address.AddressIndex;
        }

        public override int GetHashCode()
        {
            return (this.WalletId << 16) ^ (this.AccountIndex << 14) ^ (this.AddressType << 12) ^ this.AddressIndex;
        }
    }

    interface IWalletTransactionCheck
    {
        /// <summary>
        /// Determines if the outpoint has been added to this collection.
        /// </summary>
        /// <param name="outPoint">The transaction id.</param>
        /// <param name="addresses">Identifies the addresses related to the outpoint (if any).</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(OutPoint outPoint, out HashSet<AddressIdentifier> addresses);

        /// <summary>
        /// Call this after all tentative outpoints have been committed to the wallet.
        /// </summary>
        void Confirm();

        /// <summary>
        /// Call this to add outpoints paying to any of our addresses.
        /// </summary>
        /// <param name="outPoint">The transaction id to add.</param>
        /// <param name="address">An address to relate to the outpoint.</param>
        void AddTentative(OutPoint outPoint, AddressIdentifier address);

        /// <summary>
        /// Adds all outpoints found in the wallet or wallet account.
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
        public bool Contains(OutPoint outPoint, out HashSet<AddressIdentifier> addresses)
        {
            return base.Contains(outPoint.ToBytes(), out addresses) ?? Exists(outPoint, out addresses);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            base.Confirm(o => { var x = new OutPoint(); x.FromBytes(o); return this.Exists(x, out _); });
        }

        /// <inheritdoc />
        public void AddTentative(OutPoint outPoint, AddressIdentifier address)
        {
            base.AddTentative(outPoint.ToBytes(), address);
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

        private bool Exists(OutPoint outPoint, out HashSet<AddressIdentifier> addresses)
        {
            addresses = new HashSet<AddressIdentifier>(
                this.conn.Query<AddressIdentifier>($@"
                SELECT  WalletId
                ,       AccountIndex
                ,       AddressType
                ,       AddressIndex
                ,       ScriptPubKey
                FROM    HDTransactionData
                WHERE   OutputTxId = ?
                AND     OutputIndex = ? {
                // Restrict to wallet if provided.
                // "BETWEEN" boosts performance from half a seconds to 2ms.
                ((this.walletId != null) ? $@"
                AND     WalletId BETWEEN {this.walletId} AND {this.walletId}" : "")}",
                outPoint.Hash.ToString(),
                outPoint.N));

            return addresses.Count != 0;
        }
    }
}
