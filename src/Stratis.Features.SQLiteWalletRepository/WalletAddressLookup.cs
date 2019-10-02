using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// A wallet-specific or shared address lookup.
    /// </summary>
    /// <remarks>
    /// Shared lookups can't provide wallet-specific information.
    /// </remarks>
    internal class WalletAddressLookup : BaseLookup, IWalletAddressLookup
    {
        private readonly DBConnection conn;
        private int? walletId;

        internal WalletAddressLookup(DBConnection conn, int? walletId) :
            // Create a bigger hash table if its shared.
            // TODO: Make this configurable.
            base(conn.Repository.DatabasePerWallet? 20 : 26)
        {
            this.conn = conn;
            this.walletId = walletId;
        }

        /// <inheritdoc />
        public bool Contains(Script scriptPubKey, out AddressIdentifier address)
        {
            address = null;

            var res = base.Contains(scriptPubKey.ToBytes(), out HashSet<AddressIdentifier> addresses);
            if (res != null)
            {
                if ((bool)res) address = addresses.First();

                return (bool)res;
            }

            return Exists(scriptPubKey, out address);
        }

        /// <inheritdoc />
        public void Confirm()
        {
            Confirm(o => this.Exists(new Script(o), out _));
        }

        /// <inheritdoc />
        public void AddTentative(Script scriptPubKey, AddressIdentifier address)
        {
            base.AddTentative(scriptPubKey.ToBytes(), address);
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

        private bool Exists(Script scriptPubKey, out AddressIdentifier address)
        {
            string hex = scriptPubKey.ToHex();

            address = this.conn.FindWithQuery<AddressIdentifier>($@"
                        SELECT  WalletId
                        ,       AccountIndex
                        ,       AddressType
                        ,       AddressIndex
                        ,       ScriptPubKey
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
