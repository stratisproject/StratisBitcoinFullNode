using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    interface IWalletAddressCheck
    {
        /// <summary>
        /// Determines the "tentative" or "may exist" collections or the wallet itself contains
        /// the given public key hash script.
        /// </summary>
        /// <param name="scriptPubKey">The public key hash script of the address.</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(Script scriptPubKey);

        /// <summary>
        /// Addresses from the "tentative" collection are moved to the "may exist" collection
        /// if they appear in the wallet after the wallet updates have been committed.
        /// </summary>
        void Confirm();

        /// <summary>
        /// Top-up addresses that have to be added to the "may exist" collection are added here.
        /// </summary>
        /// <param name="scriptPubKey">The address to add.</param>
        void AddTentative(Script scriptPubKey);

        /// <summary>
        /// Looks in the given account for addresses to add to the "may exist" collection.
        /// </summary>
        /// <param name="walletId">The wallet to look in.</param>
        /// <param name="accountIndex">The account to look in.</param>
        void AddAll(int? walletId = null, int? accountIndex = null);
    }

    /// <summary>
    /// A wallet-specific or shared address lookup.
    /// </summary>
    /// <remarks>
    /// Shared lookups can't provide wallet-specific information.
    /// </remarks>
    internal class AddressesOfInterest : ObjectsOfInterest, IWalletAddressCheck
    {
        private readonly DBConnection conn;
        private int? walletId;

        internal AddressesOfInterest(DBConnection conn, int? walletId) :
            // Create a bigger hash table if its shared.
            // TODO: Make this configurable.
            base(conn.Repository.DatabasePerWallet? 20 : 26)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        /// <inheritdoc />
        public bool Contains(Script scriptPubKey)
        {
            return Contains(scriptPubKey.ToBytes()) ?? Exists(scriptPubKey);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            Confirm(o => this.Exists(new Script(o)));
        }

        /// <inheritdoc />
        public void AddTentative(Script scriptPubKey)
        {
            this.AddTentative(scriptPubKey.ToBytes());
        }

        /// <inheritdoc />
        public void AddAll(int? walletId = null, int? accountIndex = null)
        {
            Guard.Assert((walletId ?? this.walletId) == (this.walletId ?? walletId));

            walletId = this.walletId ?? walletId;

            List<HDAddress> addresses = this.conn.Query<HDAddress>($@"
                SELECT  *
                FROM    HDAddress {
                // Restrict to wallet if provided.
                ((walletId != null) ? $@"
                WHERE   WalletId = {walletId}" : "")} {
                // Restrict to account if provided.
                ((accountIndex != null) ? $@"
                AND     AccountIndex = {accountIndex}" : "")}");

            foreach (HDAddress address in addresses)
            {
                this.Add(Script.FromHex(address.ScriptPubKey));
            }
        }

        private void Add(Script scriptPubKey)
        {
            this.Add(scriptPubKey.ToBytes());
        }

        private bool Exists(Script scriptPubKey)
        {
            string hex = scriptPubKey.ToHex();

            bool res = this.conn.ExecuteScalar<int>($@"
                        SELECT EXISTS(
                            SELECT  1
                            FROM    HDAddress
                            WHERE   ScriptPubKey = ? {
                    // Restrict to wallet if provided.
                    ((this.walletId != null) ? $@"
                            AND     WalletId = {this.walletId}" : "")}
                            LIMIT   1);", hex) == 1;

            return res;
        }
    }
}
