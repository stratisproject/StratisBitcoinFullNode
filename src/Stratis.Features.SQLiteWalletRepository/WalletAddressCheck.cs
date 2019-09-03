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
        bool Contains(Script scriptPubKey, out HDAddress address);

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
        /// <param name="addressType">The address type to look at.</param>
        void AddAll(int? walletId = null, int? accountIndex = null, int? addressType = null);
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
        public bool Contains(Script scriptPubKey, out HDAddress address)
        {
            address = null;

            return Contains(scriptPubKey.ToBytes()) ?? Exists(scriptPubKey, out address);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            Confirm(o => this.Exists(new Script(o), out _));
        }

        /// <inheritdoc />
        public void AddTentative(Script scriptPubKey)
        {
            this.AddTentative(scriptPubKey.ToBytes());
        }

        /// <inheritdoc />
        public void AddAll(int? walletId = null, int? accountIndex = null, int? addressType = null)
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
                AND     AccountIndex = {accountIndex}" : "")} {
                // Restrict to account if provided.
                ((addressType != null) ? $@"
                AND     AddressType = {addressType}" : "")}");

            foreach (HDAddress address in addresses)
            {
                this.Add(Script.FromHex(address.ScriptPubKey));
            }
        }

        private void Add(Script scriptPubKey)
        {
            this.Add(scriptPubKey.ToBytes());
        }

        private bool Exists(Script scriptPubKey, out HDAddress address)
        {
            string hex = scriptPubKey.ToHex();

            address = this.conn.FindWithQuery<HDAddress>($@"
                        SELECT *
                        FROM    HDAddress
                        WHERE   ScriptPubKey = '{hex}' {
                    // Restrict to wallet if provided.
                    // "BETWEEN" boosts performance from half a seconds to 2ms.
                    ((this.walletId != null) ? $@"
                        AND     WalletId BETWEEN {this.walletId} AND {this.walletId}" : "")};");

            return address != null;
        }
    }
}
