using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface IWalletTransactionLookup
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
        /// Call this to add tentative outpoints paying to any of our addresses.
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
}
