using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface IWalletAddressReadOnlyLookup
    {
        /// <summary>
        /// Determines if the address has been added to this collection.
        /// </summary>
        /// <param name="scriptPubKey">The public key hash script of the address.</param>
        /// <param name="address">An address identifier.</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(Script scriptPubKey, out AddressIdentifier address);
    }

    public interface IWalletAddressLookup
    {
        /// <summary>
        /// Call this after all tentative addresses have been committed to the wallet.
        /// </summary>
        void Confirm();

        /// <summary>
        /// Call this to add tentative addresses. Used when topping-up addresses.
        /// </summary>
        /// <param name="scriptPubKey">The address to add.</param>
        /// <param name="address">The address identifier.</param>
        void AddTentative(Script scriptPubKey, AddressIdentifier address);

        /// <summary>
        /// Adds all addresses found in the wallet or wallet account..
        /// </summary>
        /// <param name="walletId">The wallet to look in.</param>
        /// <param name="accountIndex">The account to look in.</param>
        /// <param name="addressType">The address type to look at.</param>
        void AddAll(int? walletId = null, int? accountIndex = null, int? addressType = null);
    }
}
